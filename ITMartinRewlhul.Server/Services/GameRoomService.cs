using ITMartinRewlhul.Server.Data;

namespace ITMartinRewlhul.Server.Services;

// All game state lives here, in memory, guarded by one lock - a party game
// for 1-6 people in the same room doesn't need a database or a distributed
// state store, and keeping it this simple matches the "small fun app" brief.
public sealed class GameRoomService(RewlhulBroadcastService broadcast)
{
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no O/0/I/1
    private const int PadCount = 4;
    private const int RevealStepMs = 700;

    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly Dictionary<string, System.Threading.Timer> _timers = new();
    private readonly object _lock = new();

    public GameRoom CreateRoom(string hostName)
    {
        lock (_lock)
        {
            string code;
            do { code = GenerateCode(); } while (_rooms.ContainsKey(code));

            var room = new GameRoom { Code = code };
            room.Players.Add(new PlayerState { Name = hostName.Trim() });
            _rooms[code] = room;
            return room;
        }
    }

    public GameRoom? TryGetRoom(string code)
    {
        lock (_lock)
        {
            return _rooms.GetValueOrDefault(code.ToUpperInvariant());
        }
    }

    public string? JoinRoom(string code, string name)
    {
        code = code.ToUpperInvariant();
        name = name.Trim();
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return "Koden findes ikke.";
            if (room.Phase != "Lobby") return "Spillet er allerede i gang.";
            if (room.Players.Count >= 6) return "Rummet er fuldt (max 6 spillere).";
            if (room.Players.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return "Det navn er allerede taget.";
            if (string.IsNullOrWhiteSpace(name)) return "Skriv et navn.";

            room.Players.Add(new PlayerState { Name = name });
            room.LastActivity = DateTime.UtcNow;
        }
        broadcast.Broadcast(code);
        return null;
    }

    public void StartGame(string code)
    {
        code = code.ToUpperInvariant();
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return;
            if (room.Phase != "Lobby" || room.Players.Count < 1) return;
            room.Sequence.Clear();
        }
        AdvanceLevel(code);
    }

    // Grows the sequence by one pad, resets everyone's progress, enters the
    // Reveal phase, and schedules the flip to Attempt once the sequence has
    // finished playing on every client.
    private void AdvanceLevel(string code)
    {
        int stepCount;
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return;
            room.Sequence.Add(Random.Shared.Next(PadCount));
            foreach (var p in room.Players) p.Progress = 0;
            room.Phase = "Reveal";
            room.LastActivity = DateTime.UtcNow;
            stepCount = room.Sequence.Count;

            if (_timers.TryGetValue(code, out var old)) old.Dispose();
            _timers[code] = new System.Threading.Timer(_ => FlipToAttempt(code), null,
                stepCount * RevealStepMs + 400, System.Threading.Timeout.Infinite);
        }
        broadcast.Broadcast(code);
    }

    private void FlipToAttempt(string code)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return;
            if (room.Phase != "Reveal") return; // room may have reset/ended already
            room.Phase = "Attempt";
        }
        broadcast.Broadcast(code);
    }

    // Returns true if this tap ended the run (wrong pad).
    public void SubmitTap(string code, string playerName, int padIndex)
    {
        code = code.ToUpperInvariant();
        bool everyoneDone = false;
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return;
            if (room.Phase != "Attempt") return;

            var player = room.Players.FirstOrDefault(p => p.Name == playerName);
            if (player is null || player.Progress >= room.Sequence.Count) return;

            if (room.Sequence[player.Progress] != padIndex)
            {
                // Wrong tap - the whole room's run ends together (cooperative,
                // not elimination: one mistake ends it for everyone).
                room.Phase = "GameOver";
                if (_timers.TryGetValue(code, out var t)) t.Dispose();
                broadcast.Broadcast(code);
                return;
            }

            player.Progress++;
            everyoneDone = room.Players.All(p => p.Progress >= room.Sequence.Count);
        }

        broadcast.Broadcast(code);
        if (everyoneDone) AdvanceLevel(code);
    }

    public void PlayAgain(string code)
    {
        code = code.ToUpperInvariant();
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room)) return;
            room.Sequence.Clear();
            foreach (var p in room.Players) p.Progress = 0;
            room.Phase = "Lobby";
        }
        broadcast.Broadcast(code);
    }

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 4).Select(_ => CodeChars[Random.Shared.Next(CodeChars.Length)]).ToArray());
}
