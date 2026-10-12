using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Asks every price site about one item and keeps what they said (2026-10-10, user: "as many places as possible
/// ... facts are needed like 'found on X til 45 kr. Link'"). No AI - only the sites' own listings, each with its link.</summary>
public sealed class PriceHuntUseCases(IItemRepository items, IPriceRepository prices, IEnumerable<IPriceSource> sources, TimeProvider clock)
{
    private readonly List<IPriceSource> _sources = sources.ToList();

    public int SourceCount => _sources.Count;

    /// <summary>"Brugt: DBA, eBay, Tradera (Sverige) ... Ny: Bog &amp; idé."</summary>
    public string SourceList
    {
        get
        {
            static string Name(IPriceSource s) => s.Country == "" ? s.Name : $"{s.Name} ({s.Country})";
            static string And(List<string> n) => n.Count <= 1 ? string.Join("", n) : string.Join(", ", n.Take(n.Count - 1)) + " og " + n[^1];
            return $"Brugt: {And(_sources.Where(s => !s.IsNew).Select(Name).ToList())}. Ny: {And(_sources.Where(s => s.IsNew).Select(Name).ToList())}.";
        }
    }

    /// <summary>Returns how many sites answered. The sites are all different, so they are asked at the same time;
    /// each site keeps its own limit of requests at a time.</summary>
    public async Task<int> HuntAsync(int itemId, CancellationToken ct = default)
    {
        var item = await items.GetAsync(itemId, ct);
        if (item is null) return 0;
        var title = Pricing.TitleFor(item);
        var isbn = Pricing.Isbn(item.Barcode);
        var asked = _sources.Where(s => s.Fits(item.Kind)).Select(s => (s, s.LookupAsync(title, item.Artist, isbn, ct))).ToList();
        var answers = new List<SourceAnswer>();
        foreach (var (source, lookup) in asked)
        {
            Listings? r = null;
            // A site that times out throws a "cancelled" too - that is "did not answer", not the app shutting down
            // (2026-10-12: one slow Faraos search stopped the whole price hunter until the next restart).
            try { r = await lookup; } catch (Exception) when (!ct.IsCancellationRequested) { }
            answers.Add(new SourceAnswer(source.Name, source.IsNew, r));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var result = HuntResult.From(itemId, answers, now);
        await prices.ReplaceAsync(itemId, result.Checks, result.Finds, ct);
        result.ApplyTo(item, now);
        await items.UpdateAsync([item], ct);
        return result.Answered;
    }

    /// <summary>The next few: never priced first, then the ones priced more than 30 days ago.</summary>
    public Task<List<int>> NextAsync(int count, CancellationToken ct = default) =>
        items.NextToHuntAsync(Pricing.HuntedKinds, clock.GetUtcNow().UtcDateTime - Pricing.StaleAfter, count, ct);
}
