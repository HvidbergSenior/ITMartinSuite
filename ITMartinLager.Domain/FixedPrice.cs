namespace ITMartinLager.Domain;

// Fixed price per kind (user 2026-11-24): "i want to have this standard price BUT if a product is found to be higher on faraos and/or
// dba and the other relevant, then I need to set the price" + "dba should not be counted if other prices found. If no other then
// reference dba" + chose "typical price >= 2x standard and >= 50 kr".
// So an ordinary thing gets NO hint (= the shop shows Shop.StandardFor, the fixed price of its kind). Only when the typical used price
// found is clearly higher, the hint is that price - and a hint of 50 kr or more makes the shop say "vi vurderer prisen" until Martin
// sets the price himself (ShopDeals.Assess, AssessFromKr = 50).
public static class FixedPrice
{
    /// <summary>Used shops/markets that count. DBA only when none of the others found the thing (its asking prices run high).</summary>
    public static readonly string[] Sources = ["Faraos", "GulogGratis", "Nemos Bibliotek", "Discogs", "AbeBooks", "Accord"];
    public const string Dba = "DBA";
    public const int Factor = 2;
    public const int MinKr = 50;

    /// <summary>Kinds that follow the fixed price. The rest keep their own way (Magic, Ting, box sets, console games ...).</summary>
    public static bool Applies(Item i) =>
        !i.Rare && i.Kind != Kinds.Magic && i.Kind != Kinds.Ting && !Pricing.FromAdsKind(i) && StandardOf(i) is not null;

    /// <summary>The kind's fixed price; an Anders And-blad is the 3 kr bin.</summary>
    public static int? StandardOf(Item i) => i.Kind == Kinds.AndersAnd ? 3 : Shop.StandardFor(i.Kind, i.Category);

    /// <summary>The typical (middle) used price found: the other sources first, DBA only when they found nothing. Null = nothing found.</summary>
    public static int? Typical(IEnumerable<PriceFind> finds)
    {
        var used = finds.Where(f => !f.IsNew && !f.Similar && f.Kr > 0).ToList();
        var others = used.Where(f => Sources.Contains(f.Source)).Select(f => f.Kr).ToList();
        var list = others.Count > 0 ? others : used.Where(f => f.Source == Dba).Select(f => f.Kr).ToList();
        if (list.Count == 0) return null;
        list.Sort();
        return list.Count % 2 == 1 ? list[list.Count / 2] : (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2;
    }

    /// <summary>True when Martin must set the price: typical found price >= 2x the fixed price and >= 50 kr.</summary>
    public static bool NeedsOwnPrice(Item i, IEnumerable<PriceFind> finds) =>
        Applies(i) && i.Price is not > 0 && Typical(finds) is { } t && StandardOf(i) is { } s && t >= Factor * s && t >= MinKr;

    /// <summary>The hint for a thing that follows the fixed price: the typical price when Martin must decide, else none (the shop
    /// then shows the fixed price). "Som ny" keeps its own rule (25 % under the new price) when that is higher. Not applicable =
    /// the hint is left as it was.</summary>
    public static decimal? Hint(Item i, IEnumerable<PriceFind> finds, decimal? current)
    {
        if (!Applies(i)) return current;
        var list = finds.ToList();
        decimal? hint = NeedsOwnPrice(i, list) ? Pricing.OnStep(Typical(list)!.Value) : null;
        if (Pricing.LikeNew(i.Usage) && Pricing.AsNewPrice(NewTypical(list)) is { } asNew && asNew > (hint ?? 0)) hint = asNew;
        return hint;
    }

    private static int? NewTypical(List<PriceFind> finds)
    {
        var fresh = finds.Where(f => f.IsNew && !f.Similar && f.Kr > 0).Select(f => f.Kr).OrderBy(k => k).ToList();
        return fresh.Count == 0 ? null : fresh[fresh.Count / 2];
    }
}
