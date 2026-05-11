using System.Text.Json;
using ArmsFair.Server.Data;
using ArmsFair.Server.Hubs;
using ArmsFair.Server.Simulation;
using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ArmsFair.Server.Services;

/// <summary>
/// Single authority for phase transitions. Absorbs the logic previously split
/// between GameHub.AdvancePhase() and TickerService's direct hub sends.
/// </summary>
public class PhaseOrchestrator(
    IHubContext<GameHub> hub,
    IServiceScopeFactory scopeFactory,
    GameStateService gameStateService,
    ILogger<PhaseOrchestrator> logger)
{
    // Adjacency map loaded once at first use.
    private static Dictionary<string, string[]>? _adjacency;
    private static readonly object _adjLock = new();

    /// <summary>
    /// Called by TickerService when a phase timer expires.
    /// Reads state from GameStateService, advances, writes it back.
    /// </summary>
    public async Task AdvanceForGameAsync(string gameId)
    {
        if (!gameStateService.TryGet(gameId, out var state)) return;

        gameStateService.ClearReady(gameId);
        var voters  = gameStateService.GetOrAddVoters(gameId);
        var pending = gameStateService.GetAndClearPendingForGame(gameId, state.Players.Select(p => p.Id));

        var (newState, ending) = await AdvanceAsync(gameId, state, voters, pending);
        gameStateService.Set(gameId, newState);

        if (ending is not null)
            gameStateService.Remove(gameId);
    }

    /// <summary>
    /// Advances <paramref name="state"/> by one phase and returns the updated state.
    /// Broadcasts PhaseStart (and Reveal/Consequences during Reveal entry) to the SignalR group.
    /// Persists phase + round to the GameSessions table.
    /// </summary>
    public async Task<(GameState State, EndingCondition? Ending)> AdvanceAsync(
        string gameId,
        GameState state,
        HashSet<string> ceaseFireVoters,
        List<PlayerAction> pendingActions)
    {
        var nextPhase = NextPhase(state.Phase);
        var nextRound = nextPhase == GamePhase.WorldUpdate ? state.Round + 1 : state.Round;

        state = state with { Phase = nextPhase, Round = nextRound };

        // ── Phase-specific simulation ─────────────────────────────────────────
        if (nextPhase == GamePhase.WorldUpdate)
            state = RunWorldUpdate(state, pendingActions);

        if (nextPhase == GamePhase.Reveal)
            (state, pendingActions) = await RunRevealAsync(gameId, state, pendingActions);

        // ── Broadcast PhaseStart ──────────────────────────────────────────────
        long endsAt = DateTimeOffset.UtcNow
            .AddMilliseconds(PhaseDuration(nextPhase))
            .ToUnixTimeMilliseconds();

        await hub.Clients.Group(gameId).SendAsync("PhaseStart",
            new PhaseStartMessage(nextPhase, nextRound, endsAt));

        logger.LogInformation("Game {GameId} → {Phase} round {Round}", gameId, nextPhase, nextRound);

        // ── Persist ───────────────────────────────────────────────────────────
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db     = scope.ServiceProvider.GetRequiredService<ArmsFairDb>();
            var entity = await db.GameSessions.FindAsync(gameId);
            if (entity is not null)
            {
                entity.Phase     = nextPhase.ToString();
                entity.Round     = nextRound;
                entity.StateJson = JsonSerializer.Serialize(state);
                entity.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        // ── Ending check ──────────────────────────────────────────────────────
        var ending = EndingChecker.Check(state, ceaseFireVoters);
        if (ending is not null)
        {
            await TriggerEndingAsync(gameId, state, ending);
            return (state with { EndingType = ending.Type }, ending);
        }

        return (state, null);
    }

    // ── WorldUpdate: spread + blowback queuing ───────────────────────────────

    private GameState RunWorldUpdate(GameState state, List<PlayerAction> pendingActions)
    {
        var adjacency = LoadAdjacency();
        var rng       = Random.Shared;

        var spreadEvents   = new List<SpreadEvent>();
        var countryChanges = new List<CountryChange>();
        var updatedCountries = state.Countries.ToDictionary(c => c.Iso);

        // Count sales per country for this round (used in spread chance calc)
        var salesPerCountry = pendingActions
            .Where(a => a.TargetCountry is not null)
            .GroupBy(a => a.TargetCountry!)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var country in state.Countries)
        {
            if (country.Stage < CountryStage.HotWar) continue;

            var neighbors = adjacency.TryGetValue(country.Iso, out var n) ? n : [];
            int salesInto = salesPerCountry.GetValueOrDefault(country.Iso, 0);
            bool highInstability = state.Tracks.Instability >= Balance.SpreadHighInstabilityThresh;

            var spreadChance = SpreadEngine.ComputeSpreadChance(
                salesIntoZone:         salesInto,
                treatySignatories:     0,   // treaty system deferred
                peacekeepingInvestors: 0,
                isStage4:              country.Stage == CountryStage.HumanitarianCrisis,
                highInstability:       highInstability);

            foreach (var neighborIso in SpreadEngine.ComputeSpreads(country.Iso, neighbors, spreadChance, rng))
            {
                if (!updatedCountries.TryGetValue(neighborIso, out var neighbor)) continue;
                if (neighbor.Stage >= CountryStage.FailedState) continue;

                var oldStage = neighbor.Stage;
                var newStage = (CountryStage)((int)oldStage + 1);
                updatedCountries[neighborIso] = neighbor with { Stage = newStage };

                spreadEvents.Add(new SpreadEvent(country.Iso, neighborIso, (int)newStage));
                countryChanges.Add(new CountryChange(neighborIso, (int)oldStage, (int)newStage, neighbor.Tension));
            }
        }

        state = state with { Countries = [.. updatedCountries.Values] };

        // Broadcast the WorldUpdate summary (fire-and-forget — caller awaits AdvanceAsync)
        _ = hub.Clients.Group(state.GameId).SendAsync("WorldUpdate", new WorldUpdateMessage(
            TrackDeltas    : new TrackDeltas(0, 0, 0, 0, 0),
            NewTracks      : state.Tracks,
            SpreadEvents   : spreadEvents,
            CountryChanges : countryChanges,
            Events         : []));

        return state;
    }

    // ── Reveal: apply actions, compute profit + blowback, broadcast ──────────

    private async Task<(GameState State, List<PlayerAction> Cleared)> RunRevealAsync(
        string gameId, GameState state, List<PlayerAction> pendingActions)
    {
        var revealed = pendingActions
            .Select(a =>
            {
                var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
                return new RevealedAction(
                    a.PlayerId,
                    player?.CompanyName ?? a.PlayerId,
                    a.SaleType,
                    a.TargetCountry,
                    a.WeaponCategory);
            }).ToList();

        var animations = revealed
            .Where(r => r.TargetIso is not null)
            .Select((r, i) => new ArcAnimation(r.PlayerId, r.TargetIso!, r.SaleType.ToString(), i * 300))
            .ToList();

        await hub.Clients.Group(gameId).SendAsync("Reveal", new RevealMessage(revealed, animations));

        // Compute track effects + profit
        var profitUpdates = new List<ProfitUpdate>();
        var repUpdates    = new List<ReputationUpdate>();
        var blowbacks     = new List<BlowbackEvent>();
        var tracks        = state.Tracks;

        foreach (var action in pendingActions)
        {
            if (action.WeaponCategory is null || action.TargetCountry is null) continue;
            var weapon  = action.WeaponCategory.Value;
            var country = state.Countries.FirstOrDefault(c => c.Iso == action.TargetCountry);
            if (country is null) continue;

            try
            {
                tracks = action.SaleType switch
                {
                    SaleType.Open        => TrackEngine.ApplyOpenSale(tracks, weapon, country.Stage, action.IsDualSupply),
                    SaleType.Covert      => TrackEngine.ApplyCovertSale(tracks, weapon, country.Stage, action.IsDualSupply),
                    SaleType.AidCover    => TrackEngine.ApplyAidCoverSale(tracks, weapon, country.Stage),
                    SaleType.PeaceBroker => TrackEngine.ApplyPeaceBroker(tracks),
                    _                    => tracks
                };
            }
            catch (ArgumentOutOfRangeException ex)
            {
                logger.LogError(ex, "[Reveal] Unknown weapon category {Weapon} for player {Player} — skipping action", weapon, action.PlayerId);
                continue;
            }

            var player = state.Players.FirstOrDefault(p => p.Id == action.PlayerId);
            if (player is null) continue;

            var unitProfit = ProfitEngine.Calculate(
                weapon, country.Stage, action.SaleType,
                action.IsDualSupply, tracks.MarketHeat, relationshipPoints: 0);
            var profit = unitProfit * action.Quantity;

            profitUpdates.Add(new ProfitUpdate(action.PlayerId, profit, player.Capital + profit));

            if (action.SaleType == SaleType.Covert)
            {
                var traceChance = BlowbackEngine.ComputeTraceChance(
                    weapon, action.SaleType,
                    isAidExposed:  false,
                    isGrayChannel: action.IsProxyRouted,
                    isVostok:      false,
                    highLatentRisk: player.LatentRisk > 20,
                    stage:         country.Stage);

                if (BlowbackEngine.IsTraced(traceChance, Random.Shared))
                {
                    tracks = TrackEngine.ApplyCovertTrace(tracks);
                    var repLoss = BlowbackEngine.ComputeRepLoss(weapon, action.SaleType, action.IsDualSupply);
                    repUpdates.Add(new ReputationUpdate(
                        action.PlayerId, repLoss, player.Reputation + repLoss, "covert_traced"));
                    blowbacks.Add(new BlowbackEvent(action.PlayerId, action.TargetCountry, weapon, Traced: true));
                }
            }
        }

        // Apply all deltas back to authoritative PlayerProfile list
        var updatedPlayers = state.Players.Select(p =>
        {
            var profit = profitUpdates.Where(u => u.PlayerId == p.Id).Sum(u => u.ProfitEarned);
            var repDelta = repUpdates.Where(u => u.PlayerId == p.Id).Sum(u => u.Delta);
            // PeaceBroker costs $2M and earns 1 peace credit
            var peaceBrokerActs = pendingActions.Count(a =>
                a.PlayerId == p.Id && a.SaleType == SaleType.PeaceBroker);
            var peaceCost = peaceBrokerActs * Balance.PeaceCostToPlayer;
            var peaceCredits = peaceBrokerActs * Balance.PeaceCreditEarned;
            // Latent risk from covert/gray sales
            var latentGain = pendingActions
                .Where(a => a.PlayerId == p.Id)
                .Sum(a => a.SaleType == SaleType.Covert
                    ? (a.IsProxyRouted ? Balance.LatentPerGrayChannel : Balance.LatentPerCovert)
                    : 0);

            return p with
            {
                Capital      = p.Capital + profit - peaceCost,
                Reputation   = Math.Clamp(p.Reputation + repDelta
                                  + (peaceBrokerActs > 0 ? Balance.RepGainPeaceBroker * peaceBrokerActs : 0)
                                  + (pendingActions.Any(a => a.PlayerId == p.Id
                                         && a.SaleType == SaleType.Open) ? Balance.RepLossOpenSale : 0),
                               0, 100),
                PeaceCredits = p.PeaceCredits + peaceCredits,
                LatentRisk   = p.LatentRisk   + latentGain,
                Status       = p.Reputation + repDelta <= Balance.CollapseThreshold ? "collapsed" : p.Status
            };
        }).ToList();

        state = state with { Tracks = tracks, Players = updatedPlayers };

        await hub.Clients.Group(gameId).SendAsync("Consequences", new ConsequencesMessage(
            profitUpdates,
            repUpdates,
            SharePriceUpdates : [],
            BlowbackEvents    : blowbacks,
            HumanCostEvents   : [],
            TreatyResolutions : [],
            NewTracks         : tracks));

        return (state, []);
    }

    // ── Ending ────────────────────────────────────────────────────────────────

    private async Task TriggerEndingAsync(string gameId, GameState state, EndingCondition ending)
    {
        var scores = state.Players.Select(p => new FinalScore(
            p.Id, p.CompanyName ?? p.Username ?? p.Id,
            Profit    : p.Capital,
            Reputation: p.Reputation,
            Composite : p.Capital * p.Reputation / 100L,
            Legacy    : p.PeaceCredits * 10L)).ToList();

        await hub.Clients.Group(gameId).SendAsync("GameEnding",
            new GameEndingMessage(ending.Type, $"Ending: {ending.Type}", scores));

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db     = scope.ServiceProvider.GetRequiredService<ArmsFairDb>();
            var entity = await db.GameSessions.FindAsync(gameId);
            if (entity is not null)
            {
                entity.IsComplete = true;
                entity.EndingType = ending.Type;
                entity.UpdatedAt  = DateTime.UtcNow;
                entity.StateJson  = JsonSerializer.Serialize(state with { EndingType = ending.Type });
                await db.SaveChangesAsync();
            }
        }

        logger.LogInformation("Game {GameId} ended: {EndingType}", gameId, ending.Type);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GamePhase NextPhase(GamePhase current) => current switch
    {
        GamePhase.WorldUpdate  => GamePhase.Procurement,
        GamePhase.Procurement  => GamePhase.Negotiation,
        GamePhase.Negotiation  => GamePhase.Sales,
        GamePhase.Sales        => GamePhase.Reveal,
        GamePhase.Reveal       => GamePhase.Consequences,
        GamePhase.Consequences => GamePhase.WorldUpdate,
        _                      => GamePhase.WorldUpdate
    };

    private static int PhaseDuration(GamePhase phase) => phase switch
    {
        GamePhase.WorldUpdate  => Balance.PhaseWorldUpdate,
        GamePhase.Procurement  => Balance.PhaseProcurement,
        GamePhase.Negotiation  => Balance.PhaseNegotiation,
        GamePhase.Sales        => Balance.PhaseSales,
        GamePhase.Reveal       => Balance.PhaseReveal,
        GamePhase.Consequences => Balance.PhaseConsequences,
        _                      => Balance.PhaseWorldUpdate
    };

    private static Dictionary<string, string[]> LoadAdjacency()
    {
        if (_adjacency is not null) return _adjacency;
        lock (_adjLock)
        {
            if (_adjacency is not null) return _adjacency;
            var path = Path.Combine(AppContext.BaseDirectory, "GeoData", "adjacency.json");
            var json = File.ReadAllText(path);
            _adjacency = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
                ?? throw new InvalidOperationException("Failed to load adjacency.json");
        }
        return _adjacency;
    }
}
