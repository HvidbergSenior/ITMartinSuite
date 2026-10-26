using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Asks every price site about one item and keeps what they said (2026-10-10, user: "as many places as possible
/// ... facts are needed like 'found on X til 45 kr. Link'"). No AI - only the sites' own listings, each with its link.</summary>
public sealed class PriceHuntUseCases(IItemRepository items, IPriceRepository prices, IEnumerable<IPriceSource> sources, TimeProvider clock)
{
    private readonly List<IPriceSource> _sources = sources.ToList();

    public int SourceCount => _sources.Select(s => s.Name).Distinct().Count();   // Discogs is one site with a source per format

    /// <summary>"Brugt: DBA, eBay, Tradera (Sverige) ... Ny: Bog &amp; idé."</summary>
    public string SourceList
    {
        get
        {
            static string Name(IPriceSource s) => s.Country == "" ? s.Name : $"{s.Name} ({s.Country})";
            static string And(List<string> n) => n.Count <= 1 ? string.Join("", n) : string.Join(", ", n.Take(n.Count - 1)) + " og " + n[^1];
            return $"Brugt: {And(_sources.Where(s => !s.IsNew).Select(Name).Distinct().ToList())}. Ny: {And(_sources.Where(s => s.IsNew).Select(Name).Distinct().ToList())}.";
        }
    }

    /// <summary>Returns how many sites answered. The sites are all different, so they are asked at the same time;
    /// each site keeps its own limit of requests at a time.</summary>
    public async Task<int> HuntAsync(int itemId, CancellationToken ct = default)
    {
        var item = await items.GetAsync(itemId, ct);
        if (item is null) return 0;
        var isbn = Pricing.Isbn(item.Barcode);
        var answers = await AskAllAsync(item, Pricing.TitleFor(item), isbn, ct);
        if (isbn == "" && answers.All(a => a.Result is not { Count: > 0 }) && Pricing.AlternativeTitleFor(item) is { } alt)
            answers = await AskAllAsync(item, alt, isbn, ct);
        // Still nothing: the series + number, the series + title, the short title - one at a time, stop at the first hit.
        if (isbn == "")
            foreach (var broader in Pricing.FallbackTitlesFor(item))
            {
                if (answers.Any(a => a.Result is { Count: > 0 })) break;
                answers = await AskAllAsync(item, broader, isbn, ct);
            }
        // Still nothing for exactly this one: what does the same thing cost in another årgang/issue ("magen til")?
        List<SourceAnswer>? similar = null;
        if (answers.All(a => a.Result is not { HasPrices: true } || a.IsNew) && Pricing.SimilarTitleFor(item) is { } like)
            similar = await AskAllAsync(item, like, "", ct);
        // Still nothing: other issues of the same series ("Basserne 612" is not for sale, Nemos has 30 other Basserne).
        if (answers.All(a => a.Result is not { HasPrices: true } || a.IsNew) && (similar ?? []).All(a => a.Result is not { HasPrices: true } || a.IsNew)
            && Pricing.SeriesItemFor(item) is { } series)
            // "Basserne" alone is "too broad to know it is this one" - for a series that is the point, so the listings are
            // filtered as usual (other things named Basserne out) and kept.
            similar = (await AskAllAsync(series, Pricing.TitleFor(series), "", ct))
                .Select(a => a.Result is { Ambiguous: true } r ? a with { Result = Pricing.OnlyThisItem(series, ListingParse.Summarise(r.Broad, r.Url), a.Source) } : a)
                .ToList();

        var now = clock.GetUtcNow().UtcDateTime;
        var result = HuntResult.From(itemId, answers, now, similar);
        await prices.ReplaceAsync(itemId, result.Checks, result.Finds, ct);
        result.ApplyTo(item, now);
        await items.UpdateAsync([item], ct);
        return result.Answered;
    }

    private async Task<List<SourceAnswer>> AskAllAsync(Item item, string title, string isbn, CancellationToken ct)
    {
        // A film is searched WITH its format (2026-10-25: "Guardians of the Galaxy" alone found LEGO and costumes, and Danish
        // sellers seldom write DVD in the title): the format goes where the author would, so "dvd Guardians of the Galaxy".
        var format = Pricing.SearchFormat(item.Kind);
        var asked = _sources.Where(s => s.Fits(item.Kind))
            .Select(s => (s, s.LookupAsync(title, format != "" && !Pricing.FormatSure.Contains(s.Name) ? format : item.Artist, isbn, ct))).ToList();
        var answers = new List<SourceAnswer>();
        foreach (var (source, lookup) in asked)
        {
            Listings? r = null;
            // A site that times out throws a "cancelled" too - that is "did not answer", not the app shutting down
            // (2026-10-12: one slow Faraos search stopped the whole price hunter until the next restart).
            try { r = await lookup; } catch (Exception) when (!ct.IsCancellationRequested) { }
            // Sets, graded copies, other series out. New shops too when they were searched by title (2026-10-25: Bog & idé
            // gave "Søde Sally Sukkertop" for "Som i et æventyr") - an ISBN answer is the book itself.
            if (r is not null && (!source.IsNew || isbn == "")) r = Pricing.OnlyThisItem(item, r, source.Name, searchedWithFormat: format != "");
            answers.Add(new SourceAnswer(source.Name, source.IsNew, r));
        }
        return answers;
    }

    /// <summary>The next few: never priced first, then the ones priced more than 30 days ago.</summary>
    public Task<List<int>> NextAsync(int count, CancellationToken ct = default) =>
        items.NextToHuntAsync(Pricing.HuntedKinds, clock.GetUtcNow().UtcDateTime - Pricing.StaleAfter, count, ct);
}
