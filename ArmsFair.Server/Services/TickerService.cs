using ArmsFair.Server.Hubs;
using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace ArmsFair.Server.Services;

/// <summary>
/// Hosted background service that fires phase-advance ticks for each active game.
/// Games register themselves with StartGame / StopGame.
/// </summary>
public class TickerService(
    IHubContext<GameHub> hub,
    ILogger<TickerService> logger) : BackgroundService
{
    // gameId → time when the current phase expires
    private readonly ConcurrentDictionary<string, DateTimeOffset> _phaseEnds = new();
    // gameId → current phase (needed to calculate next phase duration)
    private readonly ConcurrentDictionary<string, GamePhase> _phases = new();

    private const int TickIntervalMs = 1_000;

    public void StartGame(string gameId, GamePhase initialPhase = GamePhase.WorldUpdate)
    {
        _phases[gameId]   = initialPhase;
        _phaseEnds[gameId] = DateTimeOffset.UtcNow.AddMilliseconds(PhaseDuration(initialPhase));
        logger.LogInformation("TickerService: started game {GameId} at phase {Phase}", gameId, initialPhase);
    }

    public void StopGame(string gameId)
    {
        _phases.TryRemove(gameId, out _);
        _phaseEnds.TryRemove(gameId, out _);
        logger.LogInformation("TickerService: stopped game {GameId}", gameId);
    }

    public void SetPhase(string gameId, GamePhase phase)
    {
        _phases[gameId]    = phase;
        _phaseEnds[gameId] = DateTimeOffset.UtcNow.AddMilliseconds(PhaseDuration(phase));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TickerService: started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TickIntervalMs, stoppingToken);

            var now     = DateTimeOffset.UtcNow;
            var expired = _phaseEnds
                .Where(kvp => kvp.Value <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var gameId in expired)
            {
                if (!_phases.TryGetValue(gameId, out var current)) continue;

                var next      = NextPhase(current);
                var nextRound = next == GamePhase.WorldUpdate
                    ? GetRound(gameId) + 1
                    : GetRound(gameId);

                SetPhase(gameId, next);

                long endsAt = _phaseEnds[gameId].ToUnixTimeMilliseconds();

                logger.LogInformation(
                    "TickerService: game {GameId} → {Phase} (round {Round})", gameId, next, nextRound);

                // Tell the hub group to advance — hub handles state mutation and broadcasting
                await hub.Clients.Group(gameId).SendAsync(
                    "PhaseStart",
                    new { Phase = next.ToString(), Round = nextRound, EndsAt = endsAt },
                    stoppingToken);
            }
        }

        logger.LogInformation("TickerService: stopped");
    }

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

    // Round tracking is delegated to the hub; TickerService only needs a monotonic counter.
    // We use a separate dictionary to avoid coupling to hub internals.
    private readonly ConcurrentDictionary<string, int> _rounds = new();
    private int GetRound(string gameId) => _rounds.GetOrAdd(gameId, 0);
    private void IncrementRound(string gameId) => _rounds.AddOrUpdate(gameId, 1, (_, r) => r + 1);
}
