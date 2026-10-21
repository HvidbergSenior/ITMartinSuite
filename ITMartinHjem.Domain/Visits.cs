using System.Web;

namespace ITMartinHjem.Domain;

// One page view on itmartin.dk or in one of the apps (/admin/tal). Replaced stats-web 2026-10-01.
// Host = "itmartin.dk" for the website, else the app's host. Source = where the visitor came from (Facebook, Google …).
// Visitor = random id from the browser (no cookie, no IP stored). Seconds = time on the page (website only).
public sealed class Hit
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Host { get; set; } = "";
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public string RefHost { get; set; } = "";
    public string Device { get; set; } = "";
    public string Visitor { get; set; } = "";
    public int Seconds { get; set; }

    // Hits without a browser id count one each.
    public string VisitorKey => Visitor.Length > 0 ? Visitor : "#" + Id;
}

// Hashed IPs Martin has used while logged in - hits from them are not counted (his own visits).
// "v:" + hash = one of his browsers (the browser id, not the IP).
public sealed class OwnerIp
{
    public int Id { get; set; }
    public string Hash { get; set; } = "";
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

// What a page view says about itself (wwwroot/t.js on the site, kolibri.js in the apps).
public sealed record HitRequest(string? Path, string? Title, string? Referrer, string? VisitorId, string? Host);

public static class VisitRules
{
    public const string Site = "itmartin.dk";
    // bogshoppen.dk is served by this app too and Lager runs on lager.bogshoppen.dk (user 2026-10-06: "also bogshoppen").
    public static readonly string[] Domains = [Site, "bogshoppen.dk"];
    public const int KeepDays = 400;
    public const int MaxSeconds = 3600;
    // The time on the page can only be set on a fresh hit: the id cannot be used to rewrite old numbers.
    public static readonly TimeSpan SecondsWindow = TimeSpan.FromHours(2);

    // The counted domain a host belongs to (itmartin.dk, x.itmartin.dk -> itmartin.dk), or null when it is not ours.
    public static string? DomainOf(string host) =>
        Domains.FirstOrDefault(d => host.Equals(d, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

    /// <summary>The hit to store, or null when it is not counted (a bot, or not one of our sites).</summary>
    public static Hit? ToHit(HitRequest req, string userAgent)
    {
        if (userAgent.Length == 0 || Bot(userAgent)) return null;
        var host = Norm(req.Host ?? "");
        if (DomainOf(host) is null) return null;
        var refHost = Uri.TryCreate(req.Referrer, UriKind.Absolute, out var r) ? Norm(r.Host) : "";
        return new Hit
        {
            Host = host,
            Path = Cut(StripQuery(req.Path ?? "/"), 200),
            Title = Cut(req.Title ?? "", 120),
            RefHost = Cut(refHost, 80),
            Source = SourceOf(req.Path ?? "", refHost, host),
            Device = userAgent.Contains("iPad") || userAgent.Contains("Tablet") ? "tablet" : userAgent.Contains("Mobile") ? "mobil" : "pc",
            Visitor = VisitorId(req.VisitorId),
        };
    }

    public static string VisitorId(string? id) => Cut(id ?? "", 40);

    // ?ref=facebook or ?utm_source=… on a link wins; else the referring site; else "Direkte".
    public static string SourceOf(string path, string refHost, string host)
    {
        var q = path.IndexOf('?') is var i and >= 0 ? HttpUtility.ParseQueryString(path[(i + 1)..]) : null;
        var tag = q?["ref"] ?? q?["utm_source"];
        if (!string.IsNullOrWhiteSpace(tag)) return Named(tag.ToLowerInvariant()) ?? Cut(tag.Trim().ToLowerInvariant(), 40);
        if (refHost.Length == 0) return "Direkte";
        if (refHost == host) return "Samme side";
        if (refHost == Site) return "itmartin.dk";
        if (refHost.EndsWith("." + Site)) return "Mine apps";
        return Named(refHost) ?? refHost;
    }

    private static string? Named(string s) =>
        s.Contains("facebook") || s == "fb" || s.StartsWith("fb.") || s.Contains("messenger") ? "Facebook"
        : s.Contains("instagram") ? "Instagram"
        : s.Contains("google") ? "Google"
        : s.Contains("bing") ? "Bing"
        : s.Contains("duckduckgo") ? "DuckDuckGo"
        : s.Contains("linkedin") || s == "lnkd.in" ? "LinkedIn"
        : s is "t.co" or "x.com" or "twitter.com" ? "X / Twitter"
        : s.Contains("youtube") ? "YouTube"
        : s.Contains("mail") ? "Mail"
        : null;

    private static bool Bot(string ua) =>
        ua.Contains("bot", StringComparison.OrdinalIgnoreCase) || ua.Contains("crawl", StringComparison.OrdinalIgnoreCase)
        || ua.Contains("spider", StringComparison.OrdinalIgnoreCase) || ua.Contains("Headless", StringComparison.OrdinalIgnoreCase)
        || ua.Contains("preview", StringComparison.OrdinalIgnoreCase);

    // www.itmartin.dk and martin.itmartin.dk are the same website.
    private static string Norm(string host)
    {
        host = host.Trim().ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];
        return host == "martin." + Site ? Site : host;
    }

    // Keep ?g=gallery-style app keys, drop tracking tags so one page is one row.
    private static string StripQuery(string path)
    {
        var i = path.IndexOf('?');
        if (i < 0) return path;
        var q = HttpUtility.ParseQueryString(path[(i + 1)..]);
        foreach (var k in q.AllKeys.Where(k => k is null || k == "ref" || k.StartsWith("utm_") || k == "fbclid" || k == "gclid").ToList()) q.Remove(k);
        return q.Count == 0 ? path[..i] : path[..i] + "?" + q;
    }

    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n];

    // ── The numbers on /admin/tal. Days count from Danish midnight: "I dag" = since 00:00 here.
    public static DateTime SinceUtc(int days) =>
        TimeZoneInfo.ConvertTimeToUtc(Dk.Today.AddDays(1 - days), TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"));

    // One phone/PC per Danish day.
    public static int DayVisitors(IEnumerable<Hit> hits) => hits.Select(h => (Dk.Local(h.At).Date, h.VisitorKey)).Distinct().Count();

    public static int Visitors(IEnumerable<Hit> hits, int days) => DayVisitors(hits.Where(h => h.At >= SinceUtc(days)));
}
