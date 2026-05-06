using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using System.Collections.Concurrent;

namespace ArmsFair.Server.Services;

/// <summary>
/// Hosted background service that fires phase-advance ticks for each active game.
/// Delegates all state mutation and broadcasting to PhaseOrchestrator.
/// </summary>
public class TickerService(
    PhaseOrchestrator phaseOrchestrator,
    ILogger<TickerService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _phaseEnds = new();

    private const int TickIntervalMs = 1_000;

    public void StartGame(string gameId, GamePhase initialPhase = GamePhase.WorldUpdate)
    {
        _phaseEnds[gameId] = DateTimeOffset.UtcNow.AddMilliseconds(PhaseDuration(initialPhase));
        logger.LogInformation("TickerService: started game {GameId} at phase {Phase}", gameId, initialPhase);
    }

    public void StopGame(string gameId)
    {
        _phaseEnds.TryRemove(gameId, out _);
        logger.LogInformation("TickerService: stopped game {GameId}", gameId);
    }

    public void SetPhase(string gameId, GamePhase phase)
    {
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
                // Remove timer first — PhaseOrchestrator will set the next one via SetPhase
                _phaseEnds.TryRemove(gameId, out _);

                logger.LogInformation("TickerService: advancing game {GameId}", gameId);

                try
                {
                    await phaseOrchestrator.AdvanceForGameAsync(gameId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TickerService: error advancing game {GameId}", gameId);
                }
            }
        }

        logger.LogInformation("TickerService: stopped");
    }

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
}
