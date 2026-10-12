using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>A site that can list its dearest listings first for a search (DBA's "Pris: høj-lav").</summary>
public interface IDearestListings
{
    string Name { get; }
    Task<List<Offer>> DearestAsync(string query, int pages, CancellationToken ct);
}

public interface IGoldRepository
{
    Task ReplaceAsync(string category, IReadOnlyList<GoldEntry> entries, CancellationToken ct);
    Task<List<GoldEntry>> AllAsync(CancellationToken ct);
}

/// <summary>The gold list: the dearest single issues per category, renewed weekly, and "is this one gold?".</summary>
public sealed class GoldUseCases(IEnumerable<IDearestListings> sites, IGoldRepository gold, TimeProvider clock)
{
    public const int Pages = 10;   // silver and bronze sit further down the "dearest first" list
    private List<GoldEntry>? _cache;

    public async Task<List<GoldEntry>> AllAsync(CancellationToken ct = default) => _cache ??= await gold.AllAsync(ct);

    /// <summary>Asks every site for the dearest listings in each category and keeps the top 50 issues. Returns how many.</summary>
    public async Task<int> RefreshAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var total = 0;
        foreach (var c in Gold.Categories)
        {
            var entries = new List<GoldEntry>();
            foreach (var site in sites)
            {
                List<Offer> offers;
                try { offers = await site.DearestAsync(c.Query, Pages, ct); }
                catch (Exception) when (!ct.IsCancellationRequested) { continue; }   // a site down = try next week
                entries.AddRange(Gold.Pick(c, offers, site.Name, now));
            }
            if (entries.Count == 0) continue;                  // keep last week's list rather than an empty one
            var top = entries.GroupBy(e => e.Tier).SelectMany(t => t.OrderByDescending(e => e.Kr).Take(Gold.PerTier))
                .OrderByDescending(e => e.Kr).ToList();
            await gold.ReplaceAsync(c.Name, top, ct);
            total += top.Count;
        }
        _cache = null;
        return total;
    }

    public async Task<GoldEntry?> MatchAsync(Item i, CancellationToken ct = default) =>
        Gold.Match(await AllAsync(ct), i.Kind, i.Title, i.Series, i.Number, i.Year);

    public async Task<List<GoldEntry?>> MatchAsync(IReadOnlyList<Found> found, CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        return found.Select(f => Gold.Match(all, Kinds.Normalise(f.Kind), f.Title, f.Series, f.Number, f.Year)).ToList();
    }
}
