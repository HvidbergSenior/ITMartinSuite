using ITMartinHjemmeside.Domain;

namespace ITMartinHjemmeside.Application;

/// <summary>What Martin does: make a new site in one go, set it up (look, domain, package, the owner's code), hand it over.</summary>
public sealed class Sites(ISiteStore store, IPictureStore pictures, TimeProvider clock)
{
    public IReadOnlyList<Site> All() => store.All().OrderByDescending(s => s.ChangedAt).ToList();

    public Site? Get(string id) => store.Get(id);

    /// <summary>The site whose domain this is (www. or not), or null - then the request is for the app itself.</summary>
    public Site? ByHost(string? host)
    {
        var h = SiteRules.NormalizeHost(host);
        if (h.Length == 0) return null;
        var bare = h.StartsWith("www.") ? h[4..] : h;
        return store.All().FirstOrDefault(s => s.Hosts.Any(x => x == h || x == bare || x == "www." + bare));
    }

    /// <summary>
    /// The quick start: name, short name, package and (optionally) the owner's code -> a site with Forside, Om os and Kontakt,
    /// ready to show on its preview address the same minute.
    /// </summary>
    public Site Create(string name, string? id, Package package, string? ownerCode, string? host)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var shortName = SiteRules.CheckId(string.IsNullOrWhiteSpace(id) ? name : id);
        if (store.Get(shortName) is not null) throw new SiteException("Kortnavnet er allerede brugt.");
        var site = Site.New(shortName, name, package, now);
        site.SetOwnerCode(ownerCode);
        if (!string.IsNullOrWhiteSpace(host)) site.SetHosts([host]);
        store.Save(site);
        return site;
    }

    public Site SetUp(string id, string name, string tagline, string accent, string phone, string mail,
        IEnumerable<string> hosts, Package package, int? price, string note, string? newOwnerCode)
    {
        var site = store.Get(id) ?? throw new SiteException("Siden findes ikke.");
        foreach (var h in hosts.Select(SiteRules.NormalizeHost).Where(h => h.Length > 0))
            if (ByHost(h) is { } other && other.Id != id) throw new SiteException($"{h} hører allerede til {other.Name}.");
        site.SetLook(name, tagline, accent, phone, mail);
        site.SetHosts(hosts);
        site.Package = package;
        if (price is < 0 or > 100_000) throw new SiteException("Prisen ser forkert ud.");
        site.Price = price;
        site.Note = SiteRules.CleanOptional(note, SiteRules.MaxNote);
        if (!string.IsNullOrWhiteSpace(newOwnerCode)) site.SetOwnerCode(newOwnerCode);
        site.ChangedAt = clock.GetUtcNow().UtcDateTime;
        store.Save(site);
        return site;
    }

    public void RemoveOwnerCode(string id)
    {
        var site = store.Get(id) ?? throw new SiteException("Siden findes ikke.");
        site.SetOwnerCode(null);
        store.Save(site);
    }

    public async Task SetLogoAsync(string id, Stream content, CancellationToken ct)
    {
        var site = store.Get(id) ?? throw new SiteException("Siden findes ikke.");
        var stored = await pictures.SaveAsync(id, content, ct) ?? throw new SiteException("Det var ikke et billede.");
        site.Logo = stored;
        store.Save(site);
    }

    public long UsedBytes(string id) => pictures.UsedBytes(id);

    public Task WriteZipAsync(string id, Stream output, CancellationToken ct) => pictures.WriteZipAsync(id, output, ct);
}
