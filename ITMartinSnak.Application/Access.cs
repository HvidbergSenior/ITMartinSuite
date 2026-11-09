using System.Collections.Concurrent;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Application;

public enum LoginResult { Admin, Wrong, Locked }

// Martin's own code (Snak:AdminCode). Rooms need no code - the link is the way in. Ten wrong tries from one address lock
// it for 15 minutes. Snak:ApiKey lets Kunder and itmartin.dk make a room.
public sealed class Access(string adminCode, string apiKey, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public string AdminToken { get; } = AccessRules.Token(adminCode, "admin");

    public bool IsAdmin(string? adminCookie) => adminCode.Length >= AccessRules.MinAdminCode && adminCookie == AdminToken;
    public bool IsApi(string? key) => apiKey.Length >= 16 && key == apiKey;

    /// <summary>Admin in this room: Martin's own code (every room), or the room's own admin password (2026-11-08, only that room).</summary>
    public bool IsRoomAdmin(Room r, string? adminCookie, string? roomCookie) =>
        IsAdmin(adminCookie) || (r.HasCode && roomCookie == r.CodeHash);

    public LoginResult Login(string? typed, string clientIp) =>
        Check(clientIp, () => adminCode.Length >= AccessRules.MinAdminCode && (typed ?? "").Trim() == adminCode);

    /// <summary>The room's own admin password - same lockout as Martin's code (10 wrong tries = 15 minutes).</summary>
    public LoginResult RoomLogin(Room r, string? typed, string clientIp) => Check(clientIp, () => RoomRules.CodeMatches(r, typed));

    private LoginResult Check(string clientIp, Func<bool> right)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (_wrong.TryGetValue(clientIp, out var w) && w.Count >= AccessRules.MaxWrongTries && now - w.First < AccessRules.WrongTriesWindow)
            return LoginResult.Locked;
        if (right()) { _wrong.TryRemove(clientIp, out _); return LoginResult.Admin; }
        _wrong.AddOrUpdate(clientIp, _ => (1, now),
            (_, old) => now - old.First > AccessRules.WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
        return LoginResult.Wrong;
    }
}
