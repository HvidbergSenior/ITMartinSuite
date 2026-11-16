using System.Collections.Concurrent;
using ITMartinHjemmeside.Domain;

namespace ITMartinHjemmeside.Application;

public enum LoginResult { Martin, Owner, Wrong, Locked }

/// <summary>
/// Two doors. Martin's code (Hjemmeside:AdminCode) opens every site; a site's own code opens only that site, and only when
/// its package lets the owner edit (Fuld hjælp = Martin makes the changes). Ten wrong tries from one address = 15 minutes' wait.
/// </summary>
public sealed class Access(string adminCode, TimeProvider clock)
{
    public const int MinAdminCode = 6, MaxWrongTries = 10;
    public static readonly TimeSpan WrongTriesWindow = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public string AdminToken { get; } = SiteRules.Token(adminCode, "martin");

    public bool IsMartin(string? cookie) => adminCode.Length >= MinAdminCode && cookie == AdminToken;

    public static string OwnerToken(Site s) => SiteRules.Token(s.OwnerCodeHash ?? "", "ejer:" + s.Id);

    /// <summary>Changing the code (or the package to Fuld) closes the owner's old cookie at once.</summary>
    public static bool IsOwner(Site s, string? cookie) => s.OwnerMayEdit && cookie == OwnerToken(s);

    public bool MayEdit(Site s, string? martinCookie, string? ownerCookie) => IsMartin(martinCookie) || IsOwner(s, ownerCookie);

    /// <summary>Martin's code works on any door; on a site's door the owner's code works too.</summary>
    public LoginResult Login(string? typed, Site? site, string clientIp)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (_wrong.TryGetValue(clientIp, out var w) && w.Count >= MaxWrongTries && now - w.First < WrongTriesWindow)
            return LoginResult.Locked;
        var code = (typed ?? "").Trim();
        LoginResult result =
            adminCode.Length >= MinAdminCode && code == adminCode ? LoginResult.Martin
            : site is { OwnerMayEdit: true } && SiteRules.HashCode(site.Id, code) == site.OwnerCodeHash ? LoginResult.Owner
            : LoginResult.Wrong;
        if (result != LoginResult.Wrong) { _wrong.TryRemove(clientIp, out _); return result; }
        _wrong.AddOrUpdate(clientIp, _ => (1, now),
            (_, old) => now - old.First > WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
        return LoginResult.Wrong;
    }
}
