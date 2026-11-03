namespace ITMartinLager.Domain;

/// <summary>One thing on a Fund list: its "worth looking into?" line (Gold.Interesting) and the price we would sell it for.</summary>
public sealed record FundRow(Item Item, string? Note, decimal? Kr);

/// <summary>A Fund session's box with how far it is: still to decide, taken home, not bought, still waiting for a price.</summary>
public sealed record FundTrip(Box Box, int Open, int Taken, int NotBought, int Waiting);

/// <summary>Fund (user 2026-11-03: "an easy take picture, search, find interesting, take the product"): the rules for the list
/// Martin picks from while standing at the genbrugsplads.</summary>
public static class FundRules
{
    /// <summary>What it would sell for: a person's price, the suggested shop price, else what the same series costs.</summary>
    public static decimal? Kr(Item i) => i.Price ?? i.PriceHint ?? i.SimilarHint;

    /// <summary>Interesting first (guld, sølv, bronze, måske), then the dearest; the ones still waiting for a price last.</summary>
    public static List<FundRow> Rows(IEnumerable<Item> items, IReadOnlyList<GoldEntry> gold) =>
        items.Select(i => new FundRow(i, Gold.Interesting(gold, i.Kind, i.Title, i.Series, i.Number, i.Year, i.Price ?? i.PriceHint, i.Want, i.Have), Kr(i)))
            .OrderBy(r => r.Item.PricesAt is null)
            .ThenBy(r => r.Note is null ? int.MaxValue : Gold.Rank(r.Note))
            .ThenByDescending(r => r.Kr ?? 0)
            .ThenBy(r => r.Item.Title)
            .ToList();

    public static FundTrip Trip(Box box, IReadOnlyCollection<Item> items) => new(box,
        items.Count(i => i.Status == Statuses.Fund),
        items.Count(i => Statuses.Ours(i.Status)),
        items.Count(i => i.Status == Statuses.IkkeKoebt),
        items.Count(i => i.Status == Statuses.Fund && i.PricesAt is null));
}
