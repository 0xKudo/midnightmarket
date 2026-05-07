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
using System.Text.Json;

namespace ArmsFair.Server.Hubs;

[Authorize]
public class GameHub(
    ArmsFairDb db,
    SeedService seedService,
    PhaseOrchestrator phaseOrchestrator,
    GameStateService gameStateService,
    TickerService ticker) : Hub
{
    // ── Connection lifecycle ─────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var gameId = GetGameId();
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
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

        if (!gameStateService.TryGet(gameId, out var state))
        {
            await Clients.Caller.SendAsync("Error", new ErrorMessage("GAME_NOT_FOUND", $"Game {gameId} does not exist."));
            return;
        }

        // Append joining player to state.Players if not already present
        var playerId = GetPlayerId();
        if (!state.Players.Any(p => p.Id == playerId))
        {
            var playerEntity = Guid.TryParse(playerId, out var guid)
                ? await db.Players.FindAsync(guid) : null;

            var profile = playerEntity is not null
                ? new PlayerProfile { Id = playerId, Username = playerEntity.Username, HomeNation = playerEntity.HomeNationIso ?? "USA" }
                : new PlayerProfile { Id = playerId, Username = "Operative", HomeNation = "USA" };

            state = state with { Players = new List<PlayerProfile>(state.Players) { profile } };
            gameStateService.Set(gameId, state);
        }

        await Clients.Caller.SendAsync("StateSync", new StateSync(state));
    }

    public async Task CreateGame(LobbySettingsMessage settings)
    {
        try
        {
            await CreateGameInternalAsync(settings);
        }
        catch (Exception ex)
        {
            await SendError("CREATE_GAME_FAILED", ex.Message);
        }
    }

    private async Task CreateGameInternalAsync(LobbySettingsMessage settings)
    {
        var gameId   = Guid.NewGuid().ToString();
        var playerId = GetPlayerId();

        var mode      = settings.GameMode;
        var countries = await seedService.GetCountriesAsync(mode, settings.CustomCountries);
        var tracks    = settings.GameMode == GameMode.Custom && settings.CustomTracks is not null
            ? settings.CustomTracks
            : WorldTracks.Initial(mode);

        var hostEntity = Guid.TryParse(playerId, out var hostGuid)
            ? await db.Players.FindAsync(hostGuid)
            : null;

        var hostProfile = hostEntity is not null
            ? new PlayerProfile
            {
                Id         = playerId,
                Username   = hostEntity.Username,
                HomeNation = hostEntity.HomeNationIso
            }
            : new PlayerProfile
            {
                Id         = playerId,
                Username   = "Host",
                HomeNation = "USA"
            };

        var state = new GameState
        {
            GameId    = gameId,
            Round     = 0,
            Phase     = GamePhase.WorldUpdate,
            Tracks    = tracks,
            Countries = countries,
            Players   = new List<PlayerProfile> { hostProfile }
        };
        gameStateService.Set(gameId, state);

        var entity = new GameSessionEntity
        {
            Id        = gameId,
            Round     = 0,
            Phase     = GamePhase.WorldUpdate.ToString(),
            StateJson = JsonSerializer.Serialize(state),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.GameSessions.Add(entity);
        await db.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        ticker.StartGame(gameId);
        await Clients.Group(gameId).SendAsync("StateSync", new StateSync(state));
    }

    public async Task StartGame(string gameId)
    {
        if (!gameStateService.TryGet(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        var hostId = GetPlayerId();
        if (state.Players.Count > 0 && state.Players[0].Id != hostId)
        { await SendError("NOT_HOST", "Only the host can start the game."); return; }

        ticker.StartGame(gameId);
        await Clients.Group(gameId).SendAsync("StateSync", new StateSync(state));
    }

    public async Task SubmitProcurement(string gameId, ProcurementMessage msg)
    {
        try
        {
            var playerId = GetPlayerId();
            if (!gameStateService.TryGet(gameId, out var state))
            { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

            if (state.Phase != GamePhase.Procurement)
            { await SendError("WRONG_PHASE", "Procurement can only be submitted during the Procurement phase."); return; }

            // Capital is stored in $M (int). Deduct cost in $M units.
            var totalCostM = msg.SelectedWeapons
                .Sum(w => WeaponCatalog.Items.First(i => i.Category == w).BaseCostMillions);

            var player = state.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null) { await SendError("PLAYER_NOT_FOUND", "Player not in game."); return; }
            if (player.Capital < totalCostM) { await SendError("INSUFFICIENT_CAPITAL", "Not enough capital."); return; }

            var updated = state.Players
                .Select(p => p.Id == playerId ? p with { Capital = p.Capital - totalCostM } : p)
                .ToList();
            state = state with { Players = updated };
            gameStateService.Set(gameId, state);

            await Clients.Caller.SendAsync("StateSync", new StateSync(state));
        }
        catch (Exception ex)
        {
            await SendError("PROCUREMENT_FAILED", ex.Message);
        }
    }

    public async Task SubmitAction(string gameId, SubmitActionMessage msg)
    {
        var playerId = GetPlayerId();
        if (!gameStateService.TryGet(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        if (state.Phase != GamePhase.Sales)
        { await SendError("WRONG_PHASE", "Actions can only be submitted during the Sales phase."); return; }

        gameStateService.SetPendingAction(Context.ConnectionId, new PlayerAction
        {
            PlayerId       = playerId,
            SaleType       = msg.SaleType,
            TargetCountry  = msg.TargetCountry,
            WeaponCategory = msg.WeaponCategory,
            SupplierId     = msg.SupplierId,
            IsDualSupply   = msg.IsDualSupply,
            IsProxyRouted  = msg.IsProxyRouted,
            Quantity       = Math.Max(1, msg.Quantity)
        });

        await Clients.Caller.SendAsync("ActionAcknowledged", new { playerId, gameId });
    }

    public async Task SendChat(string gameId, ChatMessage msg)
    {
        await Clients.Group(gameId).SendAsync("ChatMessage", msg);
    }

    public async Task VoteCeaseFire(string gameId)
    {
        var playerId = GetPlayerId();
        var voters   = gameStateService.GetOrAddVoters(gameId);
        lock (voters) voters.Add(playerId);

        await Clients.Group(gameId).SendAsync("CeaseFireVote", new { playerId, gameId });

        if (gameStateService.TryGet(gameId, out var state))
        {
            var ending = EndingChecker.Check(state, voters);
            if (ending is not null)
                await phaseOrchestrator.AdvanceForGameAsync(gameId);
        }
    }

    public async Task FundCoup(string gameId, FundCoupMessage msg)
    {
        var playerId = GetPlayerId();
        if (!gameStateService.TryGet(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        var outcome = CoupEngine.Roll(Random.Shared);
        var repLoss = CoupEngine.RepLoss(outcome);

        var geoHit    = outcome == CoupOutcome.Blowback ? Balance.CoupBlowbackGeoTension : 0;
        var newTracks = geoHit > 0
            ? (state.Tracks with { GeoTension = state.Tracks.GeoTension + geoHit }).Clamp()
            : state.Tracks;

        gameStateService.Set(gameId, state with { Tracks = newTracks });

        await Clients.Group(gameId).SendAsync("CoupResult", new
        {
            playerId,
            country = msg.TargetCountryIso,
            outcome = outcome.ToString(),
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
            proposerId     = playerId,
            participantIds = msg.ParticipantIds,
            terms          = msg.Terms,
            durationRounds = msg.DurationRounds
        });
    }

    public async Task Whistle(string gameId, WhistleMessage msg)
    {
        var playerId = GetPlayerId();
        if (!gameStateService.TryGet(gameId, out var state))
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
        if (!gameStateService.TryGet(gameId, out var state))
        { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

        var newTracks = (state.Tracks with
        {
            Stability    = state.Tracks.Stability    + Balance.PeacekeepingStabilityDelta,
            CivilianCost = state.Tracks.CivilianCost + Balance.PeacekeepingCivilianDelta
        }).Clamp();

        gameStateService.Set(gameId, state with { Tracks = newTracks });

        await Clients.Group(gameId).SendAsync("PeacekeepingInvested", new
        {
            playerId,
            country   = msg.TargetCountryIso,
            newTracks
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────────

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
}
