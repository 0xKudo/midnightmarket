using System.Collections.Concurrent;
using ArmsFair.Shared.Enums;

namespace ArmsFair.Server.Services;

public record RoomSummary(
    string  RoomId,
    string  RoomName,
    string  HostUsername,
    string  GameMode,
    string  TimerPreset,
    int     PlayerSlots,
    int     PlayerCount,
    bool    IsPrivate,
    bool    IsStarted,
    string  InviteCode,
    long    CreatedAt);

public class LobbyService
{
    private readonly ConcurrentDictionary<string, RoomRecord> _rooms = new();

    public RoomRecord CreateRoom(
        string hostPlayerId,
        string hostUsername,
        string roomName,
        int    playerSlots,
        string timerPreset,
        bool   voiceEnabled,
        bool   aiFillIn,
        bool   isPrivate,
        GameMode gameMode)
    {
        var roomId     = Guid.NewGuid().ToString();
        var inviteCode = GenerateInviteCode();

        var room = new RoomRecord(
            RoomId       : roomId,
            RoomName     : string.IsNullOrWhiteSpace(roomName) ? $"{hostUsername}'s room" : roomName,
            HostPlayerId : hostPlayerId,
            HostUsername : hostUsername,
            GameMode     : gameMode,
            TimerPreset  : timerPreset,
            VoiceEnabled : voiceEnabled,
            AiFillIn     : aiFillIn,
            IsPrivate    : isPrivate,
            PlayerSlots  : playerSlots,
            InviteCode   : inviteCode,
            IsStarted    : false,
            CreatedAt    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PlayerIds    : [hostPlayerId]);

        _rooms[roomId] = room;
        return room;
    }

    public IReadOnlyList<RoomSummary> ListOpen() =>
        _rooms.Values
            .Where(r => !r.IsPrivate && !r.IsStarted && r.PlayerIds.Count < r.PlayerSlots)
            .OrderByDescending(r => r.CreatedAt)
            .Select(ToSummary)
            .ToList();

    public RoomRecord? GetByRoomId(string roomId) =>
        _rooms.TryGetValue(roomId, out var r) ? r : null;

    public RoomRecord? GetByInviteCode(string code) =>
        _rooms.Values.FirstOrDefault(r =>
            r.InviteCode.Equals(code, StringComparison.OrdinalIgnoreCase));

    public bool TryJoin(string roomId, string playerId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return false;
        if (room.IsStarted) return false;
        if (room.PlayerIds.Count >= room.PlayerSlots) return false;

        var updated = room with { PlayerIds = [.. room.PlayerIds, playerId] };
        return _rooms.TryUpdate(roomId, updated, room);
    }

    public void MarkStarted(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
            _rooms[roomId] = room with { IsStarted = true };
    }

    /// <summary>
    /// Removes a player from the room. Deletes the room if it becomes empty.
    /// Returns true if the room was deleted.
    /// </summary>
    public bool TryLeave(string roomId, string playerId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return false;
        var remaining = room.PlayerIds.Where(id => id != playerId).ToList();
        if (remaining.Count == 0)
        {
            _rooms.TryRemove(roomId, out _);
            return true;
        }
        _rooms[roomId] = room with { PlayerIds = remaining };
        return false;
    }

    public void Remove(string roomId) => _rooms.TryRemove(roomId, out _);

    private static RoomSummary ToSummary(RoomRecord r) => new(
        r.RoomId, r.RoomName, r.HostUsername,
        r.GameMode.ToString(), r.TimerPreset,
        r.PlayerSlots, r.PlayerIds.Count,
        r.IsPrivate, r.IsStarted,
        r.InviteCode, r.CreatedAt);

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;
        return $"ARMS-{new string(Enumerable.Range(0, 4).Select(_ => chars[rng.Next(chars.Length)]).ToArray())}";
    }
}

public record RoomRecord(
    string       RoomId,
    string       RoomName,
    string       HostPlayerId,
    string       HostUsername,
    GameMode     GameMode,
    string       TimerPreset,
    bool         VoiceEnabled,
    bool         AiFillIn,
    bool         IsPrivate,
    int          PlayerSlots,
    string       InviteCode,
    bool         IsStarted,
    long         CreatedAt,
    List<string> PlayerIds);
