using ArmsFair.Shared.Models;
using System.Collections.Concurrent;

namespace ArmsFair.Server.Services;

/// <summary>
/// Singleton that owns all active in-memory game state.
/// Extracted from GameHub static fields so TickerService and PhaseOrchestrator
/// can access it without depending on the hub.
/// </summary>
public class GameStateService
{
    private readonly ConcurrentDictionary<string, GameState>       _games           = new();
    private readonly ConcurrentDictionary<string, PlayerAction>    _pending         = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _ceaseFireVoters = new();

    // ── Game state ────────────────────────────────────────────────────────────

    public bool TryGet(string gameId, out GameState state) =>
        _games.TryGetValue(gameId, out state!);

    public void Set(string gameId, GameState state) =>
        _games[gameId] = state;

    public void Remove(string gameId) =>
        _games.TryRemove(gameId, out _);

    public IEnumerable<string> ActiveGameIds => _games.Keys;

    // ── Pending actions ───────────────────────────────────────────────────────

    public void SetPendingAction(string connectionId, PlayerAction action) =>
        _pending[connectionId] = action;

    public List<PlayerAction> GetAndClearPendingForGame(string gameId, IEnumerable<string> playerIds)
    {
        var ids = playerIds.ToHashSet();
        var actions = _pending.Values.Where(a => ids.Contains(a.PlayerId)).ToList();
        foreach (var key in _pending.Where(kvp => ids.Contains(kvp.Value.PlayerId)).Select(kvp => kvp.Key).ToList())
            _pending.TryRemove(key, out _);
        return actions;
    }

    public void ClearPendingForGame(IEnumerable<string> playerIds)
    {
        var ids = playerIds.ToHashSet();
        foreach (var key in _pending.Where(kvp => ids.Contains(kvp.Value.PlayerId)).Select(kvp => kvp.Key).ToList())
            _pending.TryRemove(key, out _);
    }

    // ── Cease-fire voters ─────────────────────────────────────────────────────

    public HashSet<string> GetOrAddVoters(string gameId) =>
        _ceaseFireVoters.GetOrAdd(gameId, _ => new HashSet<string>());

    public void RemoveVoters(string gameId) =>
        _ceaseFireVoters.TryRemove(gameId, out _);
}
