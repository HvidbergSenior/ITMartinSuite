using System.Security.Cryptography;
using System.Text;
using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Services;

// Visit counting for itmartin.dk and all the apps (replaced stats-web 2026-10-01). The apps' old snippet still
// posts to stats.itmartin.dk/api/hit - that hostname now points here. No IP is stored: only a salted hash of
// the IPs Martin uses while logged in, so his own visits are left out.
public sealed class Tracker
{
    public const string Site = "itmartin.dk";
    private const int KeepDays = 400;

    private readonly IDbContextFactory<HjemDb> _dbf;
    private readonly byte[] _salt;
    private HashSet<string> _ownerIps = [];

    public Tracker(IDbContextFactory<HjemDb> dbf, IConfiguration cfg)
    {
        _dbf = dbf;
        var saltFile = Path.Combine(cfg["Hjem:DataDir"] ?? "/app/data", "ip-salt");
        if (!File.Exists(saltFile)) File.WriteAllBytes(saltFile, RandomNumberGenerator.GetBytes(32));
        _salt = File.ReadAllBytes(saltFile);
    }

    public async Task StartAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();
        _ownerIps = (await db.OwnerIps.Select(o => o.Hash).ToListAsync()).ToHashSet();   // IP hashes + "v:" browser-id hashes
        var old = DateTime.UtcNow.AddDays(-KeepDays);
        await db.Hits.Where(h => h.At < old).ExecuteDeleteAsync();
    }

    public static string ClientIp(HttpContext ctx) =>
        ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "";

    private string HashIp(string ip) =>
        Convert.ToHexString(SHA256.HashData([.. _salt, .. Encoding.UTF8.GetBytes(ip)]))[..16];

    // Called when Martin is seen logged in: from now on this IP does not count.
    public async Task MarkOwnerAsync(HttpContext ctx)
    {
        var hash = HashIp(ClientIp(ctx));
        if (_ownerIps.Contains(hash)) return;
        await using var db = await _dbf.CreateDbContextAsync();
        if (!await db.OwnerIps.AnyAsync(o => o.Hash == hash)) db.OwnerIps.Add(new OwnerIp { Hash = hash });
        await db.SaveChangesAsync();
        _ownerIps = [.. _ownerIps, hash];
    }

    public bool IsOwnerIp(HttpContext ctx) => _ownerIps.Contains(HashIp(ClientIp(ctx)));

    // Martin's own browsers (user 2026-10-05: "filter my own watchings out"). Svar on the iPhone is a home-screen app with
    // its own storage, so Safari there never looked logged in, and mobile IPs change. A browser that opened
    // itmartin.dk/tael-ikke-mig (or sent a hit while logged in) is never counted again, and its earlier hits are removed.
    private bool IsOwnerVisitor(string? vid) => !string.IsNullOrEmpty(vid) && _ownerIps.Contains("v:" + HashIp(vid));

    public async Task<int> MarkOwnerVisitorAsync(string? vid)
    {
        vid = Cut(vid ?? "", 40);
        if (vid.Length == 0) return 0;
        var hash = "v:" + HashIp(vid);
        await using var db = await _dbf.CreateDbContextAsync();
        if (!_ownerIps.Contains(hash))
        {
            if (!await db.OwnerIps.AnyAsync(o => o.Hash == hash)) db.OwnerIps.Add(new OwnerIp { Hash = hash });
            await db.SaveChangesAsync();
            _ownerIps = [.. _ownerIps, hash];
        }
        return await db.Hits.Where(h => h.Visitor == vid).ExecuteDeleteAsync();
    }

    // Returns the new hit's id (the website sends the time on the page for it later), or null when not counted.
    public async Task<long?> RecordAsync(HttpContext ctx, HitRequest req)
    {
        var ua = ctx.Request.Headers.UserAgent.ToString();
        if (ua.Length == 0 || Bot(ua)) return null;
        var host = Norm(req.Host ?? "");
        if (host.Length == 0 || host is "localhost" or "127.0.0.1" || !(host == Site || host.EndsWith("." + Site))) return null;
        if (ctx.User.IsOwner()) { await MarkOwnerAsync(ctx); await MarkOwnerVisitorAsync(req.VisitorId); return null; }
        if (IsOwnerIp(ctx) || IsOwnerVisitor(req.VisitorId)) return null;

        var refHost = "";
        if (Uri.TryCreate(req.Referrer, UriKind.Absolute, out var r)) refHost = Norm(r.Host);
        var hit = new Hit
        {
            Host = host,
            Path = Cut(StripQuery(req.Path ?? "/"), 200),
            Title = Cut(req.Title ?? "", 120),
            RefHost = Cut(refHost, 80),
            Source = SourceOf(req.Path ?? "", refHost, host),
            Device = ua.Contains("iPad") || ua.Contains("Tablet") ? "tablet" : ua.Contains("Mobile") ? "mobil" : "pc",
            Visitor = Cut(req.VisitorId ?? "", 40),
        };
        await using var db = await _dbf.CreateDbContextAsync();
        db.Hits.Add(hit);
        await db.SaveChangesAsync();
        return hit.Id;
    }

    public async Task SetSecondsAsync(long id, int seconds)
    {
        seconds = Math.Clamp(seconds, 0, 3600);
        await using var db = await _dbf.CreateDbContextAsync();
        // Only fresh hits: the id cannot be used to rewrite old numbers.
        var since = DateTime.UtcNow.AddHours(-2);
        await db.Hits.Where(h => h.Id == id && h.At > since).ExecuteUpdateAsync(x => x.SetProperty(h => h.Seconds, seconds));
    }

    // The apps' Kontrol idle check used stats-web's /api/last-seen - same answer from here.
    public async Task<Dictionary<string, DateTime>> LastSeenAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();
        return (await db.Hits.GroupBy(h => h.Host).Select(g => new { g.Key, Last = g.Max(h => h.At) }).ToListAsync())
            .ToDictionary(x => x.Key, x => x.Last);
    }

    public async Task<int> DeleteAllAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();
        return await db.Hits.ExecuteDeleteAsync();
    }

    // ?ref=facebook or ?utm_source=… on a link wins; else the referring site; else "Direkte".
    public static string SourceOf(string path, string refHost, string host)
    {
        var q = path.IndexOf('?') is var i and >= 0 ? System.Web.HttpUtility.ParseQueryString(path[(i + 1)..]) : null;
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
        var q = System.Web.HttpUtility.ParseQueryString(path[(i + 1)..]);
        foreach (var k in q.AllKeys.Where(k => k is null || k == "ref" || k.StartsWith("utm_") || k == "fbclid" || k == "gclid").ToList()) q.Remove(k);
        return q.Count == 0 ? path[..i] : path[..i] + "?" + q;
    }

    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n];
}

public sealed record HitRequest(string? Path, string? Title, string? Referrer, string? VisitorId, string? Host);
