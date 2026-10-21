using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

// Visit counting for itmartin.dk and all the apps (replaced stats-web 2026-10-01). No IP is stored: only a salted hash of
// the IPs Martin uses while logged in, so his own visits are left out. Rules in Domain/VisitRules.
public sealed class VisitUseCases(IHitRepository hits, IVisitorHasher hasher, IAppLinkRepository apps)
{
    private HashSet<string> _owner = [];   // IP hashes + "v:" browser-id hashes

    public async Task StartAsync()
    {
        _owner = (await hits.OwnerHashesAsync()).ToHashSet();
        await hits.PurgeOlderThanAsync(DateTime.UtcNow.AddDays(-VisitRules.KeepDays));
    }

    private async Task AddOwnerAsync(string hash)
    {
        if (_owner.Contains(hash)) return;
        await hits.AddOwnerHashAsync(hash);
        _owner = [.. _owner, hash];
    }

    // Called when Martin is seen logged in: from now on this IP does not count.
    public Task MarkOwnerIpAsync(string ip) => AddOwnerAsync(hasher.Hash(ip));

    // Martin's own browsers (user 2026-10-05: "filter my own watchings out"). Svar on the iPhone is a home-screen app with
    // its own storage, so Safari there never looked logged in, and mobile IPs change. A browser that opened
    // itmartin.dk/tael-ikke-mig (or sent a hit while logged in) is never counted again, and its earlier hits are removed.
    public async Task<int> MarkOwnerVisitorAsync(string? visitorId)
    {
        var vid = VisitRules.VisitorId(visitorId);
        if (vid.Length == 0) return 0;
        await AddOwnerAsync("v:" + hasher.Hash(vid));
        return await hits.DeleteByVisitorAsync(vid);
    }

    private bool IsOwner(string ip, string? visitorId) =>
        _owner.Contains(hasher.Hash(ip)) || (!string.IsNullOrEmpty(visitorId) && _owner.Contains("v:" + hasher.Hash(VisitRules.VisitorId(visitorId))));

    /// <returns>The new hit's id (the website sends the time on the page for it later), or null when not counted.</returns>
    public async Task<long?> RecordAsync(HitRequest req, string userAgent, string ip, bool loggedInOwner)
    {
        if (VisitRules.ToHit(req, userAgent) is not { } hit) return null;
        if (loggedInOwner) { await MarkOwnerIpAsync(ip); await MarkOwnerVisitorAsync(req.VisitorId); return null; }
        if (IsOwner(ip, req.VisitorId)) return null;
        return await hits.AddAsync(hit);
    }

    public Task SetSecondsAsync(long id, int seconds) =>
        hits.SetSecondsAsync(id, Math.Clamp(seconds, 0, VisitRules.MaxSeconds), DateTime.UtcNow - VisitRules.SecondsWindow);

    // The apps' Kontrol idle check used stats-web's /api/last-seen - same answer from here.
    public Task<Dictionary<string, DateTime>> LastSeenAsync() => hits.LastSeenAsync();

    public Task<int> DeleteAllAsync() => hits.DeleteAllAsync();

    // ── /admin/tal ──
    public sealed record Numbers(List<Hit> Hits, List<AppLink> Apps);

    public async Task<Numbers> NumbersAsync(int days = 90) => new(await hits.SinceAsync(DateTime.UtcNow.AddDays(-days)), await apps.AllAsync());
}
