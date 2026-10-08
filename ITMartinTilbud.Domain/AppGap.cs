namespace ITMartinTilbud.Domain;

// One chain's week: how many offers there are, how many involve the shop's app, and what you pay extra without it.
public sealed record ChainGap(
    string Chain, int Offers, int AppOffers, int AppOnly, int Priced, decimal ExtraTotal, decimal ExtraAverage, string Leaflets,
    List<GapExample> Examples);
public sealed record GapExample(string Title, decimal WithoutApp, decimal WithApp);

/// <summary>A chain's leaflet as the leaflet service lists it.</summary>
public sealed record Leaflet(string Id, string Chain, string Label, int OfferCount, DateTimeOffset From, DateTimeOffset Till);

/// <summary>"App-kløften" (user 2026-10-06): counted in the chains' own leaflets for this week.</summary>
public static class AppGap
{
    /// <summary>This week's food leaflet of a grocery chain: running now, at most 10 days long, not the nonfood one.</summary>
    public static bool IsThisWeeksFoodLeaflet(Leaflet l, DateTimeOffset now) =>
        GroceryChains.Contains(l.Chain) && l.OfferCount > 0 && l.From <= now && now <= l.Till
        && (l.Till - l.From).TotalDays <= 10 && !l.Label.Contains("nonfood", StringComparison.OrdinalIgnoreCase);

    public static ChainGap Summarise(string chain, IReadOnlyList<Offer> offers, string leaflets)
    {
        var app = offers.Where(o => o.AppName is not null).ToList();
        // Without-app minus with-app, where the leaflet says both.
        var priced = app.Select(o => o.AppPrice is { } ap ? (o, Without: o.Price, With: ap)
                                    : o.NoAppPrice is { } np ? (o, Without: np, With: o.Price) : (o, Without: 0m, With: 0m))
            .Where(x => x.Without > x.With && x.With > 0).ToList();
        var total = priced.Sum(x => x.Without - x.With);
        return new ChainGap(chain, offers.Count, app.Count, app.Count(o => o.NeedsApp is not null), priced.Count,
            Math.Round(total, 2), priced.Count == 0 ? 0 : Math.Round(total / priced.Count, 2), leaflets,
            priced.OrderByDescending(x => x.Without - x.With).Take(3).Select(x => new GapExample(x.o.Title, x.Without, x.With)).ToList());
    }

    /// <summary>The chains where the app matters most first.</summary>
    public static List<ChainGap> Ordered(IEnumerable<ChainGap> gaps) =>
        gaps.OrderByDescending(r => r.Offers == 0 ? 0 : (double)r.AppOffers / r.Offers).ThenBy(r => r.Chain).ToList();
}
