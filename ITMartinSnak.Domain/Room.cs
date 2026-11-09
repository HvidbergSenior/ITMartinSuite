using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ITMartinSnak.Domain;

// Rooms (2026-11-04, user: "a snak with every customer", "temporary chats, which will be removed after they are finished",
// "created when people contact me ... maybe even on creation of customer"). One room per customer or job. The secret key in
// the link IS the access - nobody types a code. Martin (the host) sees all rooms.
// 2026-11-08 SHARED rooms (user: a room for Vibz, Dorthe, Mette, Bent around Sonja, 85, "admin is me", "I create and make tasks",
// the room has its own admin password, only the admin needs it): Members = the names shown as buttons at the door and offered when a task is
// given to someone; CodeHash = this room's own admin password (a hash, never the code) - it makes you admin in THIS room only.
// 2026-11-09 GRUPPE (Acceleratorkursus Hold 36, replaces their Facebook group): a standing group - no "Sagen er klaret", never deleted;
// About = what each member works with (the Holdet tab), Topics = the workshops files are sorted under. Older rooms read as before.
public sealed record Room(string Id, string Key, string Title, DateTime CreatedAt, DateTime LastAt, DateTime? DoneAt, string CustomerRef,
    List<string>? Members = null, string CodeHash = "", string Slug = "", bool Gruppe = false, Dictionary<string, string>? About = null,
    List<string>? Topics = null)
{
    public bool IsDone => DoneAt is not null;
    public bool HasCode => CodeHash.Length > 0;
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

    public const int MaxMembers = 20, MinCode = 6;

    public static Room New(string? title, string? customerRef, DateTime nowUtc, string? members = null, string? code = null, string? slug = null,
        bool group = false)
    {
        var easy = CleanSlug(slug);
        if ((slug ?? "").Trim().Length > 0 && easy.Length == 0)
            throw new InvalidOperationException("Den nemme adresse må kun have små bogstaver, tal og bindestreg (2-30 tegn), fx sonja.");
        var c = (customerRef ?? "").Trim();
        var id = NewId();
        var pw = (code ?? "").Trim();
        if (pw.Length is > 0 and < MinCode) throw new InvalidOperationException($"Adgangskoden skal være mindst {MinCode} tegn.");
        return new Room(id, NewKey(), CleanTitle(title), nowUtc, nowUtc, null, c.Length > MaxCustomerRef ? c[..MaxCustomerRef] : c,
            CleanMembers(members), pw.Length == 0 ? "" : CodeHashFor(id, pw), easy, group);
    }

    // 2026-11-08 user: "make an easy url" - snak.itmartin.dk/sonja leads to the room. Anyone who guesses it gets in (said in the form).
    private static readonly HashSet<string> Reserved = ["api", "r", "om", "hjaelp", "health", "healthz", "admin", "login", "_content", "kolibri"];
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,29}$")] private static partial Regex SlugShape();

    /// <summary>"Sonja" -> "sonja"; "" when it is not a usable address.</summary>
    public static string CleanSlug(string? slug)
    {
        var s = (slug ?? "").Trim().ToLowerInvariant().Replace('æ', 'a').Replace('ø', 'o').Replace('å', 'a').Replace(' ', '-');
        return SlugShape().IsMatch(s) && !Reserved.Contains(s) ? s : "";
    }

    /// <summary>"Vibz, Dorthe; Mette" -> distinct names, max 20, each cut to the name length.</summary>
    public static List<string> CleanMembers(string? members) =>
        (members ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GroupRules.CleanName).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxMembers).ToList();

    /// <summary>The room's password as a hash (also the cookie value) - salted with the room id.</summary>
    public static string CodeHashFor(string roomId, string code) => AccessRules.Token(code.Trim(), "rum:" + roomId);

    public static bool CodeMatches(Room r, string? typed) => r.HasCode && CodeHashFor(r.Id, typed ?? "") == r.CodeHash;

    public static bool ShouldDelete(Room r, DateTime nowUtc) => r.DeleteAt is { } d && d <= nowUtc;

    /// <summary>The overview: who waits for Martin = the newest message is not his.</summary>
    public static bool Waiting(IReadOnlyList<Message> messages) => messages.Count > 0 && !messages[^1].Host;
}
