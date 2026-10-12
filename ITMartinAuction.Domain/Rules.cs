namespace ITMartinAuction.Domain;

// The bidding rules - what was inside AuctionService before the 2026-10-12 restructure, now testable on their own.
public static class Bidding
{
    public const int FirstCountdownSeconds = 30, ExtendSeconds = 15;

    /// <summary>The next live bid must be the highest live bid + 1 kr, or the starting price when there is none.</summary>
    public static decimal MinBid(AuctionItem item)
    {
        var live = item.Bids.Where(b => !b.IsPreBid).ToList();
        return live.Count > 0 ? live.Max(b => b.Amount) + 1 : item.StartingPrice;
    }

    public static Bid? TopLiveBid(AuctionItem item) => item.Bids.Where(b => !b.IsPreBid).MaxBy(b => b.Amount);

    /// <summary>When an item goes live, the highest pre-bid becomes its first live bid; the other pre-bids are dropped.
    /// Returns the promoted bid (or null) and the pre-bids to remove.</summary>
    public static (Bid? Promoted, List<Bid> Stale) PromotePreBid(AuctionItem item)
    {
        var top = item.Bids.Where(b => b.IsPreBid).MaxBy(b => b.Amount);
        if (top is null) return (null, []);
        top.IsPreBid = false;
        return (top, item.Bids.Where(b => b.IsPreBid).ToList());
    }

    /// <summary>Hammer: sold to the highest live bid - no live bid (or "not sold") = passed. Returns the winning bid.</summary>
    public static Bid? Close(AuctionItem item, bool sold)
    {
        var top = sold ? TopLiveBid(item) : null;
        if (top is null) { item.Status = AuctionItemStatus.Passed; return null; }
        item.WinnerBidderId = top.BidderId;
        item.WinningBid = top.Amount;
        item.Status = AuctionItemStatus.Sold;
        return top;
    }

    /// <summary>A passed item can be bought at once by name and phone.</summary>
    public static void BuyNow(AuctionItem item, string name, string phone)
    {
        if (item.Status != AuctionItemStatus.Passed) throw new InvalidOperationException("Genstand er ikke tilgængelig for køb nu");
        item.BuyNowBuyerName = name;
        item.BuyNowBuyerPhone = phone;
        item.Status = AuctionItemStatus.Sold;
    }
}

public static class Codes
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O, 1/I - easy to read aloud

    public static string Generate(Random rng) => new(Enumerable.Range(0, 6).Select(_ => Chars[rng.Next(Chars.Length)]).ToArray());

    public static string Normalise(string code) => code.Trim().ToUpperInvariant();
}

public static class BidderColors
{
    private static readonly string[] All = ["#f5a623", "#3498db", "#2ecc71", "#e74c3c", "#9b59b6", "#1abc9c", "#e67e22", "#e91e63"];

    public static string For(int bidderCount) => All[bidderCount % All.Length];
}

// The marks from Bogshoppen Lager, as order on the auction: gold first, then silver, bronze, maybe.
public static class Highlights
{
    public static int Rank(string? highlight) => highlight switch
    {
        null or "" => 9,
        _ when highlight.StartsWith("🏆") => 0,
        _ when highlight.StartsWith("🥈") => 1,
        _ when highlight.StartsWith("🥉") => 2,
        _ => 3,
    };

    /// <summary>The short badge for a card: "🏆 GULD", "🥈 SØLV", "🥉 BRONZE", "🔎 MÅSKE".</summary>
    public static string Badge(string highlight)
    {
        var dash = highlight.IndexOf('–');
        return (dash > 0 ? highlight[..dash] : highlight).Trim();
    }
}
