namespace ITMartinLager.Domain;

// What Lager tells Salg (2026-10-21 split - before, the whole shop lived here): which things are for sale, the mark
// buyers see, and the proof for the price promise. Lager owns the stock, so these rules stay here; the Køb nu price,
// the day offers, the orders and the payment are Salg's.
public static class Shop
{
    /// <summary>For sale: in stock or til salg, not on an auction, not sold, copies left, a title, and not unsure - an unsure
    /// reading waits for a person (user 2026-10-31, "Demo pc-spil · Anders And & Co." was a cover headline read at 0.4).
    /// Without a price it is in its own category (ShopDeals.NoPrice, user 2026-10-31: "No pricing yet. We are investigating").
    /// Exception: an unsure blad in the Blade deal is shown (user 2026-10-31 yes - the name hardly matters in a 10 for 100 bin).</summary>
    /// Since 2026-10-31 evening the unsure ones are shown too, in their own category ShopDeals.Unsure without a price
    /// (user: "there are only 461 products?" -> "show them in one category").
    public static bool Listed(Item i) => ForSale(i);

    /// <summary>The AI was unsure what it is - shown under ShopDeals.UnsureName, never with a price.</summary>
    public static bool Unsure(Item i) => i.Confidence < Pricing.MinConfidence;

    /// <summary>In stock or til salg, copies left, a title - sure or not (what DealFor looks at).</summary>
    public static bool ForSale(Item i) =>
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

    /// <summary>Which deal it is sold in (ShopDeals, user 2026-10-31), or "": gold, silver and bronze never - they are sold
    /// one by one ("Impiortant to know if some of them are interesting... If not then..."); an unsure reading never - a
    /// person checks it first ("low confidence should be manual investigation"); no price, or priced at or under the deal's
    /// MaxSingleKr = the deal of its kind (Blade, Bøger, DVD, VHS, CD); dearer things and other kinds keep their own price.
    /// An unsure blad goes straight in the Blade deal (user 2026-10-31 yes), whatever its read price.
    /// Deals = ShopDeals.Active unless given (none in use since 2026-10-31).</summary>
    public static string DealFor(Item i, IReadOnlyList<GoldEntry> gold, IReadOnlyList<ShopDeal>? deals = null)
    {
        deals ??= ShopDeals.Active;
        if (Badge(gold, i) != "") return "";
        var deal = ShopDeals.For(i.Kind, i.Series, deals);
        // A fixed price per piece (ShopDeals.PerPiece, 2026-11-02): every one that is not rare - unless it is a sure reading
        // whose exact copies cost OwnFromKr or more (then its own price, and it is on the page).
        if (deal is { EveryPrice: true })
            return deal.OwnFromKr is { } own && i.Confidence >= Pricing.MinConfidence && (i.Price ?? i.PriceHint) >= own ? "" : deal.Name;
        if (i.Confidence < Pricing.MinConfidence) return deals.Contains(ShopDeals.Blade) && ShopDeals.Blade.Kinds.Contains(i.Kind) ? ShopDeals.Blade.Name : "";
        if (deal is null) return "";
        var kr = i.Price ?? i.PriceHint;
        if (kr is > 0) return kr <= deal.MaxSingleKr ? deal.Name : "";
        return i.PricesAt is null ? "" : deal.Name;   // no price: once searched (not yet = wait for the hunter)
    }

    /// <summary>The standard price per kind when the price hunter found nothing (user 2026-11-02, "what about products without
    /// price?" - chose a fixed price per type): only for a sure reading that has been searched. Lager still says "Ingen pris
    /// fundet"; the shop shows this price. Anders And-blade are not here - they are the 3 kr bin.</summary>
    public static readonly IReadOnlyDictionary<string, int> StandardKr = new Dictionary<string, int>
    {
        [Kinds.Cd] = 10, [Kinds.Dvd] = 15, [Kinds.BluRay] = 20, [Kinds.Tegneserie] = 15, [Kinds.Superhelte] = 15,
        [Kinds.Jumbobog] = 10, [Kinds.Bog] = 20,
    };

    /// <summary>The standard price for this thing, or null (it has its own, is unsure, not searched yet, or no standard).</summary>
    public static int? Standard(Item i) =>
        i.Price is > 0 || i.PriceHint is > 0 || i.PricesAt is null || i.Confidence < Pricing.MinConfidence ? null
            : StandardKr.TryGetValue(i.Kind, out var kr) ? kr : null;

    /// <summary>A thing for sale as Salg gets it.</summary>
    public static StockItem View(Item i, IReadOnlyList<GoldEntry> gold, ShopProof? reference = null) =>
        new(i.Id, i.Kind, i.Line(), i.Title, i.Interest, i.Condition, i.Year, i.Quantity, i.Usage,
            i.Price is > 0 ? (int)Math.Round(i.Price.Value) : null, i.PriceHint is > 0 ? (int)Math.Round(i.PriceHint.Value) : Standard(i),
            i.Confidence, i.ItemPhoto != "" || i.PhotoId is not null, Badge(gold, i), i.Box?.IsTest == true, reference, i.Deal);

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
