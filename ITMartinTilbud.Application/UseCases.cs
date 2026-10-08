using ITMartinTilbud.Domain;

namespace ITMartinTilbud.Application;

/// <summary>Search one item: every chain's offer near the visitor, the right kind first, cheapest per kg/litre,
/// each with the chain's nearest store.</summary>
public sealed class SearchOffers(IOfferSource source)
{
    public async Task<List<Offer>> ExecuteAsync(string query, double? lat, double? lng, int? km, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) throw new TilbudException("Skriv mindst 2 bogstaver.");
        if (lat is null || lng is null) throw new TilbudException("Vælg først hvor du bor.");
        query = query.Trim();
        var radius = Math.Clamp(km ?? 10, 1, Geo.MaxSearchKm);

        var found = new List<Offer>();
        foreach (var term in OfferRanking.SearchTerms(query))
            found.AddRange(await source.SearchAsync(term, lat.Value, lng.Value, radius, ct));
        var merged = found.DistinctBy(o => (o.Chain, o.Title, o.Price, o.ValidTo)).ToList();
        var list = OfferRanking.Relevant(OfferRanking.Sorted(merged), query);

        var near = new Dictionary<string, NearestStore?>();
        foreach (var id in list.Select(o => o.DealerId).OfType<string>().Distinct())
            near[id] = await source.NearestStoreAsync(id, lat.Value, lng.Value, ct);
        return list.Select(o => o.DealerId is { } d && near.GetValueOrDefault(d) is { } n ? o with { NearestStore = n.Address, NearestKm = n.Km } : o).ToList();
    }
}

/// <summary>App-kløften: per chain this week, how many offers involve the app and what you pay extra without it.</summary>
public sealed class ThisWeeksAppGap(IOfferSource source, TimeProvider clock)
{
    public const int MaxOffersPerLeaflet = 600;

    public async Task<List<ChainGap>> ExecuteAsync(double lat, double lng, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var leaflets = (await source.LeafletsAsync(lat, lng, ct)).Where(l => AppGap.IsThisWeeksFoodLeaflet(l, now)).DistinctBy(l => l.Id).ToList();
        var result = new List<ChainGap>();
        foreach (var chain in leaflets.GroupBy(l => l.Chain))
        {
            var offers = new List<Offer>();
            foreach (var l in chain) offers.AddRange(await source.LeafletOffersAsync(l, ct));
            if (offers.Count > 0) result.Add(AppGap.Summarise(chain.Key, offers, string.Join(", ", chain.Select(l => l.Label))));
        }
        return AppGap.Ordered(result);
    }
}

/// <summary>Faste tilbud: everyone reads them; only Martin adds or removes (the web layer checks his PIN).</summary>
public sealed class FixedDealsBoard(IFixedDealStore store, TimeProvider clock)
{
    public IReadOnlyList<FixedDeal> All() => store.All().OrderBy(d => d.Item).ThenBy(d => d.Chain).ToList();

    public FixedDeal Add(string chain, string item, int[]? days, decimal? price, string? unit, string? needsApp, string? note)
    {
        var deal = FixedDeal.Create(chain, item, days, price, unit, needsApp, note, clock.GetUtcNow().UtcDateTime);
        store.Add(deal);
        return deal;
    }

    public bool Remove(string id) => store.Remove(id);
}

/// <summary>Madspild near the visitor (Salling's official API).</summary>
public sealed class NearbyFoodWaste(IFoodWasteSource source)
{
    public bool Enabled => source.Enabled;

    public Task<List<Clearance>> ExecuteAsync(double? lat, double? lng, int? km, CancellationToken ct)
    {
        if (lat is null || lng is null) throw new TilbudException("Vælg først hvor du bor.");
        return source.NearAsync(lat.Value, lng.Value, km ?? 5, ct);
    }
}
