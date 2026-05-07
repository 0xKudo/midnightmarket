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
    private readonly ConcurrentDictionary<string, List<PlayerAction>> _pending         = new();
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

    public void SetPendingActions(string connectionId, List<PlayerAction> actions) =>
        _pending[connectionId] = actions;

    public List<PlayerAction> GetAndClearPendingForGame(string gameId, IEnumerable<string> playerIds)
    {
        var ids = playerIds.ToHashSet();
        var result = new List<PlayerAction>();
        foreach (var key in _pending.Keys.ToList())
        {
            if (!_pending.TryGetValue(key, out var list)) continue;
            if (!list.Any(a => ids.Contains(a.PlayerId))) continue;
            result.AddRange(list.Where(a => ids.Contains(a.PlayerId)));
            _pending.TryRemove(key, out _);
        }
        return result;
    }

    public void ClearPendingForGame(IEnumerable<string> playerIds)
    {
        var ids = playerIds.ToHashSet();
        foreach (var key in _pending.Keys.ToList())
        {
            if (_pending.TryGetValue(key, out var list) && list.Any(a => ids.Contains(a.PlayerId)))
                _pending.TryRemove(key, out _);
        }
    }

    // ── Cease-fire voters ─────────────────────────────────────────────────────

    public HashSet<string> GetOrAddVoters(string gameId) =>
        _ceaseFireVoters.GetOrAdd(gameId, _ => new HashSet<string>());

    public void RemoveVoters(string gameId) =>
        _ceaseFireVoters.TryRemove(gameId, out _);
}
