using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ITMartinSnak.Domain;

// Rooms (2026-11-04, user: "a snak with every customer", "temporary chats, which will be removed after they are finished",
// "created when people contact me ... maybe even on creation of customer"). One room per customer or job. The secret key in
// the link IS the access - nobody types a code. Martin (the host) sees all rooms.
public sealed record Room(string Id, string Key, string Title, DateTime CreatedAt, DateTime LastAt, DateTime? DoneAt, string CustomerRef)
{
    public bool IsDone => DoneAt is not null;
    public DateTime? DeleteAt => DoneAt + RoomRules.KeepAfterDone;
}

public static partial class RoomRules
{
    public const int MaxTitle = 80, MaxCustomerRef = 80, MaxRooms = 2000;
    // A finished room stays this long (time for a "tak" or one last question), then everything in it is deleted.
    public static readonly TimeSpan KeepAfterDone = TimeSpan.FromDays(14);
    public const string DoneText = "Så er sagen klaret. Rummet bliver slettet om 14 dage. Er der mere, så skriv bare her igen.";
    // The room the old group chat (Rico and friends, 2026-10-09) was moved into - its push phones stay in that room only.
    public const string LegacyRoomId = "rico";

    /// <summary>24 url-safe characters (144 bits): cannot be guessed.</summary>
    public static string NewKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');

    public static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();

    [GeneratedRegex("^[A-Za-z0-9_-]{24}$")] private static partial Regex KeyShape();
    [GeneratedRegex("^[a-z0-9]{1,20}$")] private static partial Regex IdShape();
    public static bool ValidKey(string? key) => key is not null && KeyShape().IsMatch(key);
    public static bool ValidId(string? id) => id is not null && IdShape().IsMatch(id);

    public static string CleanTitle(string? title)
    {
        var t = (title ?? "").Trim();
        if (t.Length == 0) t = "Snak med Martin";
        return t.Length > MaxTitle ? t[..MaxTitle] : t;
    }

    public static Room New(string? title, string? customerRef, DateTime nowUtc)
    {
        var c = (customerRef ?? "").Trim();
        return new Room(NewId(), NewKey(), CleanTitle(title), nowUtc, nowUtc, null, c.Length > MaxCustomerRef ? c[..MaxCustomerRef] : c);
    }

    public static bool ShouldDelete(Room r, DateTime nowUtc) => r.DeleteAt is { } d && d <= nowUtc;

    /// <summary>The overview: who waits for Martin = the newest message is not his.</summary>
    public static bool Waiting(IReadOnlyList<Message> messages) => messages.Count > 0 && !messages[^1].Host;
}
