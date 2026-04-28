using ArmsFair.Server.Data;
using ArmsFair.Server.Data.Entities;
using ArmsFair.Server.Services;
using ArmsFair.Server.Simulation;
using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ArmsFair.Server.Hubs;

[Authorize]
public class GameHub(ArmsFairDb db, SeedService seedService) : Hub
{
    // In-memory game state keyed by gameId.
    // In a multi-server deployment this would live in Redis; single-server is fine for now.
    private static readonly ConcurrentDictionary<string, GameState>        _games   = new();
    private static readonly ConcurrentDictionary<string, PlayerAction>     _pending = new(); // connectionId → action
    private static readonly ConcurrentDictionary<string, HashSet<string>>  _ceaseFireVoters = new();

    // ── Connection lifecycle ─────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var playerId = GetPlayerId();
        var gameId   = GetGameId();
        if (gameId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // ── Client → Server methods ──────────────────────────────────────────────

    public async Task JoinGame(string gameId)
    {
        var playerId = GetPlayerId();
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

        if (_games.TryGetValue(gameId, out var state))
            await Clients.Caller.SendAsync("StateSync", new StateSync(state));
        else
            await Clients.Caller.SendAsync("Error", new ErrorMessage("GAME_NOT_FOUND", $"Game {gameId} does not exist."));
    }

    public async Task CreateGame(LobbySettingsMessage settings)
    {
        var gameId   = Guid.NewGuid().ToString();
        var playerId = GetPlayerId();

        var mode      = settings.GameMode;
        var countries = await seedService.GetCountriesAsync(mode, settings.CustomCountries);
        var tracks    = settings.GameMode == GameMode.Custom && settings.CustomTracks is not null
            ? settings.CustomTracks
            : WorldTracks.Initial(mode);

        var state = new GameState
        {
            GameId    = gameId,
            Round     = 0,
            Phase     = GamePhase.WorldUpdate,
            Tracks    = tracks,
            Countries = countries,
            Players   = new List<PlayerProfile>()
        };
        _games[gameId] = state;

        var entity = new GameSessionEntity
        {
            Id         = gameId,
            Round      = 0,
            Phase      = GamePhase.WorldUpdate.ToString(),
            StateJson  = JsonSerializer.Serialize(state),
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        };
        db.GameSessions.Add(entity);
        await db.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Caller.SendAsync("StateSync", new StateSync(state));
    }

    public async Task SubmitAction(string gameId, SubmitActionMessage msg)
    {
        var playerId = GetPlayerId();
        if (!_games.TryGetValue(gameId, out var state))
        {
            await SendError("GAME_NOT_FOUND", "Game not found.");
            return;
        }
        if (state.Phase != GamePhase.Sales)
        {
            await SendError("WRONG_PHASE", "Actions can only be submitted during the Sales phase.");
            return;
        }

        _pending[Context.ConnectionId] = new PlayerAction
        {
            PlayerId       = playerId,
            SaleType       = msg.SaleType,
            TargetCountry  = msg.TargetCountry,
            WeaponCategory = msg.WeaponCategory,
            SupplierId     = msg.SupplierId,
            IsDualSupply   = msg.IsDualSupply,
            IsProxyRouted  = msg.IsProxyRouted
        };

        await Clients.Caller.SendAsync("ActionAcknowledged", new { playerId, gameId });
    }

    public async Task SendChat(string gameId, ChatMessage msg)
    {
        await Clients.Group(gameId).SendAsync("ChatMessage", msg);
    }

    public async Task VoteCeaseFire(string gameId)
    {
        var playerId = GetPlayerId();
        var voters   = _ceaseFireVoters.GetOrAdd(gameId, _ => new HashSet<string>());
        lock (voters) voters.Add(playerId);

        await Clients.Group(gameId).SendAsync("CeaseFireVote", new { playerId, gameId });

        if (_games.TryGetValue(gameId, out var state))
        {
            var ending = EndingChecker.Check(state, voters);
            if (ending is not null)
                await TriggerEnding(gameId, state, ending);
        }
    }

    public async Task FundCoup(string gameId, FundCoupMessage msg)
    {
        var playerId = GetPlayerId();
        if (!_games.TryGetValue(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        var outcome = CoupEngine.Roll(Random.Shared);
        var repLoss = CoupEngine.RepLoss(outcome);

        var geoHit  = outcome == CoupOutcome.Blowback ? Balance.CoupBlowbackGeoTension : 0;
        var newTracks = geoHit > 0
            ? (state.Tracks with { GeoTension = state.Tracks.GeoTension + geoHit }).Clamp()
            : state.Tracks;

        _games[gameId] = state with { Tracks = newTracks };

        await Clients.Group(gameId).SendAsync("CoupResult", new
        {
            playerId,
            country  = msg.TargetCountryIso,
            outcome  = outcome.ToString(),
            repLoss,
            geoHit
        });

        await LogAudit(gameId, state.Round, playerId, "coup",
            new { msg.TargetCountryIso, outcome = outcome.ToString(), repLoss });
    }

    public async Task ProposeTreaty(string gameId, ProposeTreatyMessage msg)
    {
        var playerId = GetPlayerId();
        var treatyId = Guid.NewGuid().ToString();
        await Clients.Group(gameId).SendAsync("TreatyProposed", new
        {
            treatyId,
            proposerId    = playerId,
            participantIds = msg.ParticipantIds,
            terms         = msg.Terms,
            durationRounds = msg.DurationRounds
        });
    }

    public async Task Whistle(string gameId, WhistleMessage msg)
    {
        var playerId = GetPlayerId();
        if (!_games.TryGetValue(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        await Clients.Group(gameId).SendAsync("WhistleResult", new WhistleResultMessage(
            Level          : msg.Level,
            TargetName     : msg.TargetPlayerId,
            WeaponCategory : null,
            Procurement    : null,
            Action         : null,
            IsAidFraud     : false));

        await LogAudit(gameId, state.Round, playerId, $"whistle_l{msg.Level}",
            new { msg.TargetPlayerId });
    }

    public async Task InvestInPeacekeeping(string gameId, PeacekeepingMessage msg)
    {
        var playerId = GetPlayerId();
        if (!_games.TryGetValue(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        var newTracks = (state.Tracks with
        {
            Stability    = state.Tracks.Stability    + Balance.PeacekeepingStabilityDelta,
            CivilianCost = state.Tracks.CivilianCost + Balance.PeacekeepingCivilianDelta
        }).Clamp();

        _games[gameId] = state with { Tracks = newTracks };

        await Clients.Group(gameId).SendAsync("PeacekeepingInvested", new
        {
            playerId,
            country   = msg.TargetCountryIso,
            newTracks
        });
    }

    // ── Server-initiated phase advance (called by TickerService) ────────────

    public async Task AdvancePhase(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var state)) return;

        var nextPhase = state.Phase switch
        {
            GamePhase.WorldUpdate  => GamePhase.Procurement,
            GamePhase.Procurement  => GamePhase.Negotiation,
            GamePhase.Negotiation  => GamePhase.Sales,
            GamePhase.Sales        => GamePhase.Reveal,
            GamePhase.Reveal       => GamePhase.Consequences,
            GamePhase.Consequences => GamePhase.WorldUpdate,
            _                      => GamePhase.WorldUpdate
        };

        var nextRound = nextPhase == GamePhase.WorldUpdate ? state.Round + 1 : state.Round;
        _games[gameId] = state with { Phase = nextPhase, Round = nextRound };

        long endsAt = DateTimeOffset.UtcNow.AddMilliseconds(PhaseDuration(nextPhase)).ToUnixTimeMilliseconds();
        await Clients.Group(gameId).SendAsync("PhaseStart",
            new PhaseStartMessage(nextPhase, nextRound, endsAt));

        // Persist updated phase
        var entity = await db.GameSessions.FindAsync(gameId);
        if (entity is not null)
        {
            entity.Phase     = nextPhase.ToString();
            entity.Round     = nextRound;
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        if (nextPhase == GamePhase.Reveal)
            await RunReveal(gameId, _games[gameId]);

        var ending = EndingChecker.Check(_games[gameId],
            _ceaseFireVoters.GetValueOrDefault(gameId) ?? new HashSet<string>());
        if (ending is not null)
            await TriggerEnding(gameId, _games[gameId], ending);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task RunReveal(string gameId, GameState state)
    {
        var actions   = _pending.Values
            .Where(a => state.Players.Any(p => p.Id == a.PlayerId))
            .ToList();

        var revealed = actions.Select(a =>
        {
            var player = state.Players.First(p => p.Id == a.PlayerId);
            return new RevealedAction(
                a.PlayerId,
                player.CompanyName ?? a.PlayerId,
                a.SaleType,
                a.TargetCountry,
                a.WeaponCategory);
        }).ToList();

        var animations = revealed
            .Where(r => r.TargetIso is not null)
            .Select((r, i) => new ArcAnimation(r.PlayerId, r.TargetIso!, r.SaleType.ToString(), i * 300))
            .ToList();

        await Clients.Group(gameId).SendAsync("Reveal",
            new RevealMessage(revealed, animations));

        // Process track effects and profit for each action
        var profitUpdates = new List<ProfitUpdate>();
        var repUpdates    = new List<ReputationUpdate>();
        var blowbacks     = new List<BlowbackEvent>();
        var tracks        = state.Tracks;

        foreach (var action in actions)
        {
            if (action.WeaponCategory is null || action.TargetCountry is null) continue;
            var weapon  = action.WeaponCategory.Value;
            var country = state.Countries.FirstOrDefault(c => c.Iso == action.TargetCountry);
            if (country is null) continue;

            tracks = action.SaleType switch
            {
                SaleType.Open      => TrackEngine.ApplyOpenSale(tracks, weapon, country.Stage, action.IsDualSupply),
                SaleType.Covert    => TrackEngine.ApplyCovertSale(tracks, weapon, country.Stage, action.IsDualSupply),
                SaleType.AidCover  => TrackEngine.ApplyAidCoverSale(tracks, weapon, country.Stage),
                SaleType.PeaceBroker => TrackEngine.ApplyPeaceBroker(tracks),
                _ => tracks
            };

            var player = state.Players.FirstOrDefault(p => p.Id == action.PlayerId);
            if (player is null) continue;

            var relPts = 0; // relationship points — full system deferred to future task
            var profit = ProfitEngine.Calculate(
                weapon, country.Stage, action.SaleType,
                action.IsDualSupply, tracks.MarketHeat, relPts);

            profitUpdates.Add(new ProfitUpdate(action.PlayerId, profit, player.Capital + profit));

            // Covert trace check
            if (action.SaleType == SaleType.Covert)
            {
                var traceChance = BlowbackEngine.ComputeTraceChance(
                    weapon, action.SaleType,
                    isAidExposed: false, isGrayChannel: action.IsProxyRouted,
                    isVostok: false, highLatentRisk: player.LatentRisk > 20,
                    stage: country.Stage);

                if (BlowbackEngine.IsTraced(traceChance, Random.Shared))
                {
                    tracks = TrackEngine.ApplyCovertTrace(tracks);
                    var repLoss = BlowbackEngine.ComputeRepLoss(weapon, action.SaleType, action.IsDualSupply);
                    repUpdates.Add(new ReputationUpdate(
                        action.PlayerId, repLoss,
                        player.Reputation + repLoss, "covert_traced"));
                    blowbacks.Add(new BlowbackEvent(action.PlayerId, action.TargetCountry, weapon, Traced: true));
                }
            }
        }

        _games[gameId] = state with { Tracks = tracks };
        _pending.Clear();

        await Clients.Group(gameId).SendAsync("Consequences", new ConsequencesMessage(
            profitUpdates,
            repUpdates,
            SharePriceUpdates : new List<SharePriceUpdate>(),
            BlowbackEvents    : blowbacks,
            HumanCostEvents   : new List<HumanCostEvent>(),
            TreatyResolutions : new List<TreatyResolution>(),
            NewTracks         : tracks));
    }

    private async Task TriggerEnding(string gameId, GameState state, EndingCondition ending)
    {
        var scores = state.Players.Select(p => new FinalScore(
            p.Id, p.CompanyName ?? p.Id,
            Profit    : p.Capital,
            Reputation: p.Reputation,
            Composite : p.Capital * p.Reputation / 100L,
            Legacy    : p.PeaceCredits * 10L)).ToList();

        var msg = new GameEndingMessage(ending.Type, $"Ending: {ending.Type}", scores);
        await Clients.Group(gameId).SendAsync("GameEnding", msg);

        var entity = await db.GameSessions.FindAsync(gameId);
        if (entity is not null)
        {
            entity.IsComplete  = true;
            entity.EndingType  = ending.Type;
            entity.UpdatedAt   = DateTime.UtcNow;
            entity.StateJson   = JsonSerializer.Serialize(state with { EndingType = ending.Type });
            await db.SaveChangesAsync();
        }

        _games.TryRemove(gameId, out _);
        _ceaseFireVoters.TryRemove(gameId, out _);
    }

    private async Task LogAudit(string gameId, int round, string playerId, string actionType, object detail)
    {
        db.AuditLogs.Add(new AuditLogEntity
        {
            GameId     = gameId,
            Round      = round,
            PlayerId   = playerId,
            ActionType = actionType,
            DetailJson = JsonSerializer.Serialize(detail),
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SendError(string code, string message) =>
        await Clients.Caller.SendAsync("Error", new ErrorMessage(code, message));

    private string GetPlayerId() =>
        Context.User?.FindFirst("sub")?.Value
        ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? Context.ConnectionId;

    private string? GetGameId() =>
        Context.GetHttpContext()?.Request.Query["gameId"].FirstOrDefault();

    private static int PhaseDuration(GamePhase phase) => phase switch
    {
        GamePhase.WorldUpdate  => Balance.PhaseWorldUpdate,
        GamePhase.Procurement  => Balance.PhaseProcurement,
        GamePhase.Negotiation  => Balance.PhaseNegotiation,
        GamePhase.Sales        => Balance.PhaseSales,
        GamePhase.Reveal       => Balance.PhaseReveal,
        GamePhase.Consequences => Balance.PhaseConsequences,
        _                      => 30_000
    };
}
