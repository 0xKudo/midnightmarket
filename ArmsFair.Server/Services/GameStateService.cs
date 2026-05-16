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
    private readonly ConcurrentDictionary<string, GameState>          _games           = new();
    private readonly ConcurrentDictionary<string, List<PlayerAction>> _pending         = new();
    private readonly ConcurrentDictionary<string, HashSet<string>>    _ceaseFireVoters = new();
    private readonly ConcurrentDictionary<string, HashSet<string>>    _readyPlayers    = new();
    private readonly ConcurrentDictionary<string, (string GameId, string PlayerId)> _connections = new();

    // ── Game state ────────────────────────────────────────────────────────────

    public bool TryGet(string gameId, out GameState state) =>
        _games.TryGetValue(gameId, out state!);

    public void Set(string gameId, GameState state) =>
        _games[gameId] = state;

    public void Remove(string gameId) =>
        _games.TryRemove(gameId, out _);

    public IEnumerable<string> ActiveGameIds => _games.Keys;

    // ── Connection tracking ───────────────────────────────────────────────────

    public void TrackConnection(string connectionId, string gameId, string playerId) =>
        _connections[connectionId] = (gameId, playerId);

    public bool TryGetConnection(string connectionId, out string gameId, out string playerId)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
        {
            gameId   = entry.GameId;
            playerId = entry.PlayerId;
            return true;
        }
        gameId = playerId = string.Empty;
        return false;
    }

    public void RemoveConnection(string connectionId) =>
        _connections.TryRemove(connectionId, out _);

    // ── Pending actions ───────────────────────────────────────────────────────

    public void AddPendingActions(string connectionId, List<PlayerAction> actions) =>
        _pending.AddOrUpdate(connectionId, actions, (_, existing) =>
        {
            existing.AddRange(actions);
            return existing;
        });

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

    // ── Ready players ─────────────────────────────────────────────────────────

    public void MarkReady(string gameId, string playerId)
    {
        var set = _readyPlayers.GetOrAdd(gameId, _ => new HashSet<string>());
        lock (set) set.Add(playerId);
    }

    public bool AreAllReady(string gameId, IEnumerable<string> allPlayerIds)
    {
        if (!_readyPlayers.TryGetValue(gameId, out var set)) return false;
        var ids = allPlayerIds.ToList();
        if (ids.Count == 0) return false;
        lock (set) return ids.All(id => set.Contains(id));
    }

    public void ClearReady(string gameId) =>
        _readyPlayers.TryRemove(gameId, out _);
}
