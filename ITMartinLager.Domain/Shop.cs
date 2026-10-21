namespace ITMartinLager.Domain;

// What Lager tells Salg (2026-10-21 split - before, the whole shop lived here): which things are for sale, the mark
// buyers see, and the proof for the price promise. Lager owns the stock, so these rules stay here; the Køb nu price,
// the day offers, the orders and the payment are Salg's.
public static class Shop
{
    /// <summary>For sale: in stock or til salg, not on an auction, not sold, copies left, a title.</summary>
    public static bool Listed(Item i) =>
        (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg) && i.Quantity > 0 && i.Title.Trim() != "";

    /// <summary>The mark buyers see in the marketplace: "🏆 GULD", "🥈 SØLV", "🥉 BRONZE" (its own valued price, or the
    /// gold list) - never "Måske", that is for us.</summary>
    public static string Badge(IReadOnlyList<GoldEntry> gold, Item i)
    {
        var note = Gold.Interesting(gold, i.Kind, i.Title, i.Series, i.Number, i.Year, i.Price ?? i.PriceHint);
        return note is null || Gold.Rank(note) > 2 ? "" : note.Split(" – ")[0];
    }

    /// <summary>The proof for the price promise: the cheapest listing per site, exact matches only, cheapest first.</summary>
    public static List<PriceFind> Proof(IEnumerable<PriceFind> finds, int max = 6) =>
        finds.Where(f => !f.Similar && f.Kr > 0 && f.Url.StartsWith("http"))
            .GroupBy(f => (f.Source, f.IsNew))
            .Select(g => g.OrderBy(f => f.Kr).First())
            .OrderBy(f => f.IsNew).ThenBy(f => f.Kr)
            .Take(max).ToList();

    /// <summary>A thing for sale as Salg gets it.</summary>
    public static StockItem View(Item i, IReadOnlyList<GoldEntry> gold) =>
        new(i.Id, i.Kind, i.Line(), i.Title, i.Interest, i.Condition, i.Year, i.Quantity, i.Usage,
            i.Price is > 0 ? (int)Math.Round(i.Price.Value) : null, i.PriceHint is > 0 ? (int)Math.Round(i.PriceHint.Value) : null,
            i.Confidence, i.ItemPhoto != "", Badge(gold, i));

    public static ShopProof ProofView(PriceFind f) => new(f.Source, f.Kr, f.Original, f.Url, f.IsNew);
}

/// <summary>A sale from Salg that Lager has booked (its Ref = Salg's order token), so the same sale sent twice takes the
/// copies out only once.</summary>
public sealed class SaleBooking
{
    public int Id { get; set; }
    public string Ref { get; set; } = "";
    public DateTime BookedAt { get; set; } = DateTime.UtcNow;
}
