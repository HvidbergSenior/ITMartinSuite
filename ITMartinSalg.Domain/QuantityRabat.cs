namespace ITMartinSalg.Domain;

/// <summary>Mængderabat (user 2026-11-16: "a way to click on more than one type of product... 1, 5 for a price, 10 for a price, 20,
/// 50, 100" + "rabat" -> "Steps in %", "Count, no extra rabat"): any mix of things in one purchase - the more, the cheaper each.
/// The 3 kr Anders And and 5 kr VHS bins count towards the number, but their price is already the lowest: no rabat on them.
/// Worked out after the day's campaigns; a line that got a campaign keeps the better of the two.</summary>
public static class QuantityRabat
{
    public static readonly IReadOnlyList<(int From, int Percent)> Steps = [(5, 10), (10, 20), (20, 30), (50, 40), (100, 50)];

    /// <summary>The kinds whose price is a fixed bin price (ShopDeals with one price per piece) - counted, never discounted.</summary>
    public static readonly string[] BinKinds = [ITMartinVarer.Contracts.Kinds.AndersAnd, ITMartinVarer.Contracts.Kinds.Vhs];

    public static int PercentFor(int count) => Steps.LastOrDefault(s => count >= s.From).Percent;

    /// <summary>The next step after this many things: how many more, and the percent there - null at the top.</summary>
    public static (int More, int Percent)? Next(int count) =>
        Steps.FirstOrDefault(s => s.From > count) is { From: > 0 } n ? (n.From - count, n.Percent) : null;

    public static string Words(int percent) => $"Mængderabat {percent} %";

    /// <summary>The basket with the rabat on every line that may have it (whole kroner, never under 1 kr).</summary>
    public static PricedBasket Apply(PricedBasket priced, IReadOnlyList<BasketLine> basket)
    {
        var percent = PercentFor(priced.Lines.Count);
        if (percent == 0) return priced;
        var kinds = basket.ToDictionary(b => b.ItemId, b => b.Kind);
        return new PricedBasket(priced.Lines.Select(l =>
        {
            if (BinKinds.Contains(kinds.GetValueOrDefault(l.ItemId) ?? "")) return l;
            var kr = Math.Max(1, (int)Math.Floor(l.ListKr * (100 - percent) / 100m));
            return kr < l.Kr ? l with { Kr = kr, Offer = Words(percent) } : l;
        }).ToList());
    }
}
