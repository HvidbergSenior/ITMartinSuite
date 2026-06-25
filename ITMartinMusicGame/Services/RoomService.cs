using ITMartinMusicGame.Models;

namespace ITMartinMusicGame.Services;

public class RoomService
{
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly object _lock = new();

    public event Func<string, Task>? RoomChanged;

    public GameRoom? Get(string code)
    {
        lock (_lock) { return _rooms.TryGetValue(code, out var r) ? r : null; }
    }

    public GameRoom Create(string hostPlayerId, string hostName)
    {
        var code = GenerateCode();
        var room = new GameRoom
        {
            Code = code,
            HostPlayerId = hostPlayerId,
            Players = [new Player { Id = hostPlayerId, Name = hostName, IsHost = true }]
        };
        lock (_lock) { _rooms[code] = room; }
        return room;
    }

    public (bool ok, string? error) Join(string code, string playerId, string playerName)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return (false, "Rum ikke fundet");
            if (room.Phase != GamePhase.Lobby) return (false, "Spillet er allerede i gang");
            if (room.Players.Count >= 10) return (false, "Rummet er fuldt (max 10)");
            if (room.Players.Any(p => p.Id == playerId)) return (true, null);
            room.Players.Add(new Player { Id = playerId, Name = playerName });
            return (true, null);
        }
    }

    public async Task NotifyAsync(string code)
    {
        if (RoomChanged != null)
            await RoomChanged.Invoke(code);
    }

    public void AddPoints(GameRoom room, string playerId, int points)
    {
        var p = room.Players.FirstOrDefault(x => x.Id == playerId);
        if (p != null) p.Score += points;
    }

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do { code = new string(Enumerable.Range(0, 4).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray()); }
        while (_rooms.ContainsKey(code));
        return code;
    }
}
