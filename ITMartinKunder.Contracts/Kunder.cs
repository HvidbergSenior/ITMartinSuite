using System.Net.Mail;

namespace ITMartinKunder.Contracts;

// The Kundekartotek for ALL apps (user 2026-10-21): one list of customers, owned by Kunder. Every app (itmartin.dk,
// bogshoppen.dk, Stem, Salg ...) sends what happened here; none keeps its own list.
public static class KunderApi
{
    public const string KeyHeader = "X-Kunder-Key";
    public const string SignUp = "/api/kunder/tilmeld";
    public const string Bought = "/api/kunder/koeb";
    public const string Unsubscribe = "/api/kunder/afmeld";
    public const string UnsubscribeLink = "/api/kunder/afmeld-link";
    public const string Import = "/api/kunder/import";
    /// <summary>POST: is this kundenummer good for this app? (ElPriser beskeder, Stem lav din egen afstemning - user 2026-10-21)</summary>
    public const string CheckCode = "/api/kunder/kode";
    /// <summary>POST: like SignUp, but answers with the kundenummer (itmartin.dk shows it and mails it).</summary>
    public const string SignUpWithCode = "/api/kunder/tilmeld-kode";
    /// <summary>GET, open: the free apps a sign-up gives access to right now (Martin switches them on/off in Kunder).</summary>
    public const string FreeApps = "/api/gratis-apps";
    /// <summary>GET (key): the customers Martin has ticked as Hjælper - Vareinfo's session page picks a helper per box.</summary>
    public const string Helpers = "/api/kunder/hjaelpere";
}

/// <summary>A ticked helper: the kundenummer (Vareinfo's helper id is minus it) and the name.</summary>
public sealed record HelperInfo(string Code, string Name);

/// <summary>NeedsCode = the extras in the app need the kundenummer (ElPriser, Stem); false = open to all (Temperatur).</summary>
public sealed record FreeAppInfo(string Name, string Url, string Text, bool NeedsCode = true);

public sealed record SignUpResponse(string Code);
public sealed record CodeCheckRequest(string Code, string App);

/// <summary>The "app" Lager and Vareinfo ask for (user 2026-10-24: helpers log in with their kundenummer): ok only for a
/// customer Martin has ticked as Hjælper in Kunder, while their yes stands.</summary>
public static class KunderRoles
{
    public const string Helper = "Hjælper";

    /// <summary>The "app" Vareinfo and Lager ask first (2026-10-26): ok only for a customer Martin has ticked as Admin -
    /// they then open as for Martin's PIN (sessions, boxes, labels, prices).</summary>
    public const string Admin = "Admin";

    /// <summary>The marketplace on bogshoppen.dk (2026-10-26: customers log in with the kundenummer, see the things and
    /// mark them "interessant"): ok for every customer whose kundenummer works - not one of the free apps.</summary>
    public const string Market = "Bogshoppen";
}
/// <summary>Ok, or Error (Danish, for the person).</summary>
public sealed record CodeCheckResponse(bool Ok, string Name = "", string Error = "");

/// <summary>Who the customer said yes to. One yes to ITMartin covers ALL free apps (user 2026-10-21:
/// "customer should sign up for all free apps in one go"); Bogshoppen (Martin and Rico) is its own yes.</summary>
public static class Companies
{
    public const string ITMartin = "ITMartin";
    public const string Bogshoppen = "Bogshoppen";
    public static readonly IReadOnlyList<string> All = [ITMartin, Bogshoppen];
}

public static class KundeWords
{
    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>The kundenummer is typed by hand: spaces and dashes do not matter.</summary>
    public static string NormalizeCode(string? code) => new string((code ?? "").Where(char.IsDigit).ToArray());

    /// <summary>"48217730" -> "4821 7730", easier to read aloud and type.</summary>
    public static string ShowCode(string code) => code.Length == 8 ? code[..4] + " " + code[4..] : code;

    public static bool IsRealEmail(string email) =>
        MailAddress.TryCreate(email, out var ma) && ma.Address == email && email.Split('@')[1].Contains('.');

    // "Mente du …?" (user 2026-10-29, after rikkebunch@hotmaol.com signed up and then again with hotmail.com): typos in
    // the big mail providers. hotmaol.com HAS a mail server (a typo domain someone bought), so a DNS check alone misses
    // it. Short names (live, msn, me) are only checked for their ending - "life.dk" is a real domain next to "live.dk".
    private static readonly string[] Providers =
        ["gmail.com", "hotmail.com", "hotmail.dk", "outlook.com", "outlook.dk", "yahoo.com", "yahoo.dk", "icloud.com",
         "live.dk", "live.com", "msn.com", "me.com", "mail.dk", "jubii.dk", "gmail.dk",
         // real domains right next to the big ones - never "corrected"
         "ymail.com", "email.com", "email.dk", "mail.com", "gmx.com", "gmx.dk", "gmx.net", "aol.com", "mac.com", "proton.me", "protonmail.com"];
    private static readonly string[] LongNames = ["gmail", "hotmail", "outlook", "yahoo", "icloud"];
    private static readonly Dictionary<string, string> Endings = new()
        { ["con"] = "com", ["cm"] = "com", ["om"] = "com", ["comm"] = "com", ["cpm"] = "com", ["vom"] = "com", ["xom"] = "com", ["co"] = "com",
          ["dkk"] = "dk", ["dl"] = "dk", ["sk"] = "dk", ["fk"] = "dk" };

    /// <summary>The address they most likely meant ("rikke@hotmaol.com" -> "rikke@hotmail.com"), or null when it looks right.</summary>
    public static string? SuggestEmail(string? email)
    {
        var e = Normalize(email);
        var at = e.LastIndexOf('@');
        if (at < 1 || at == e.Length - 1) return null;
        var (local, domain) = (e[..at], e[(at + 1)..]);
        if (Providers.Contains(domain)) return null;
        var dot = domain.LastIndexOf('.');
        var (name, end) = dot < 0 ? (domain, "") : (domain[..dot], domain[(dot + 1)..]);
        var fixedEnd = Endings.GetValueOrDefault(end, end);
        // The ending only ("gmail.con", "live.dkk").
        if (fixedEnd != end && Providers.Contains($"{name}.{fixedEnd}")) return $"{local}@{name}.{fixedEnd}";
        // The name of a big provider, one or two letters off ("hotmaol", "gmial", "outlok").
        var close = LongNames.Where(n => n != name && Distance(n, name) <= (n.Length >= 6 ? 2 : 1)).OrderBy(n => Distance(n, name)).FirstOrDefault();
        if (close is null) return null;
        var guess = Providers.Contains($"{close}.{fixedEnd}") ? $"{close}.{fixedEnd}" : Providers.First(p => p.StartsWith(close + "."));
        return $"{local}@{guess}";
    }

    // Letters to change, add, remove or swap with the neighbour to get from a to b.
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}

/// <summary>A ticked "skriv mig op" box. ConsentText = the exact words they ticked (markedsføringsloven §10, GDPR art. 7).
/// App = where they were (kept as an interest); the yes is for the whole Company.</summary>
public sealed record SignUpRequest(string Email, string Name, string Company, string App, string Source, string ConsentText,
    string Interests, DateTime? AtUtc = null);

/// <summary>A paid order. A buyer is a customer because they bought - NOT a yes to offers.</summary>
public sealed record PurchaseRequest(string Email, string Name, string Phone, string App, string Ref, string What, int Kr, DateTime AtUtc);

public sealed record UnsubscribeRequest(string Email);
public sealed record UnsubscribeLinkRequest(string Token);
public sealed record UnsubscribeResponse(bool Found, string Email = "");
public sealed record ErrorResponse(string Error);

/// <summary>A customer from an app's old list (Stem's poll.db, 2026-10-21), with its own dates and afmeld-link,
/// so links in mails already sent keep working.</summary>
public sealed record ImportRequest(string Email, string Name, string Phone, string Company, string App, string Source,
    string ConsentText, DateTime ConsentAt, string Interests, string Note, string UnsubscribeToken, DateTime? UnsubscribedAt, DateTime CreatedAt);
