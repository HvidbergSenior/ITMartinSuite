namespace ITMartinLager.Domain;

// What Lager tells Salg (2026-10-21 split - before, the whole shop lived here): which things are for sale, the mark
// buyers see, and the proof for the price promise. Lager owns the stock, so these rules stay here; the Køb nu price,
// the day offers, the orders and the payment are Salg's.
public static class Shop
{
    /// <summary>For sale: in stock or til salg, not on an auction, not sold, copies left, a title - and a price (user
    /// 2026-10-31: only priced things on bogshoppen.dk/produkter; the rest waits for the price hunter).</summary>
    public static bool Listed(Item i) =>
        (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg) && i.Quantity > 0 && i.Title.Trim() != "" && (i.Price ?? i.PriceHint) > 0;

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

    /// <summary>The one link shown on every product (user 2026-10-31: "there need to be a reference to something on all
    /// products"): the cheapest exact listing, used first - what our price is set under; else one "magen til". Never eBay
    /// (user 2026-10-29: "Dont use ebay at all"). Our price is set UNDER the other sites, so the reference is the cheapest listing at or above our price (2026-10-31:
    /// "25 kr - lignende koster 1 kr" worked against us); listings under MinReferenceKr are start bids or junk.</summary>
    public static ShopProof? Reference(IEnumerable<PriceFind> finds, decimal? ourKr = null)
    {
        var real = finds.Where(f => f.Kr >= MinReferenceKr && f.Kr >= (ourKr ?? 0) && f.Url.StartsWith("http")
            && !f.Source.Equals("eBay", StringComparison.OrdinalIgnoreCase)).ToList();
        var best = real.Where(f => !f.Similar).OrderBy(f => f.IsNew).ThenBy(f => f.Kr).FirstOrDefault()
            ?? real.Where(f => f.Similar).OrderBy(f => f.IsNew).ThenBy(f => f.Kr).FirstOrDefault();
        return best is null ? null : ProofView(best);
    }

    public const int MinReferenceKr = 5;

    /// <summary>A thing for sale as Salg gets it.</summary>
    public static StockItem View(Item i, IReadOnlyList<GoldEntry> gold, ShopProof? reference = null) =>
        new(i.Id, i.Kind, i.Line(), i.Title, i.Interest, i.Condition, i.Year, i.Quantity, i.Usage,
            i.Price is > 0 ? (int)Math.Round(i.Price.Value) : null, i.PriceHint is > 0 ? (int)Math.Round(i.PriceHint.Value) : null,
            i.Confidence, i.ItemPhoto != "", Badge(gold, i), i.Box?.IsTest == true, reference);

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
