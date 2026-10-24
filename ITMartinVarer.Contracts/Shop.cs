namespace ITMartinVarer.Contracts;

// The shop API (/api/butik/*) between Lager (the stock) and Hjem (bogshoppen.dk shows it). Key header X-Shop-Key.
// Never anything a buyer should not see: no boxes, notes, buyers or purchase prices.
public static class ShopHeaders
{
    public const string Key = "X-Shop-Key";
}

/// <summary>One thing in the shop. BuyNowKr null = no Køb nu yet (ExpectKr = what it is worth on the other sites,
/// minus 10 %). Available = copies not reserved by someone paying right now. Type = ProductTypes. DayKr = today's
/// price when a day offer covers it (DayOffer says which). Badge = "🏆 GULD" / "🥈 SØLV" / "🥉 BRONZE" or "". Usage = Usages.
/// ForShow = from a test box (user 2026-10-24): shown, never sold - no Køb nu, no kurv.</summary>
public sealed record ShopItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int? BuyNowKr, int? ExpectKr, int Available, bool HasPhoto, string Type = "", int? DayKr = null, string DayOffer = "", string Badge = "",
    string Usage = "", bool ForShow = false);

/// <summary>The proof for the price promise: one listing on another site.</summary>
public sealed record ShopProof(string Source, int Kr, string Original, string Url, bool IsNew);

// A sale from payment to the buyer having it (user 2026-10-20: "sold, packed, in storage or something").
public static class OrderStatuses
{
    public const string Pending = "Afventer betaling", Paid = "Betalt", Packed = "Pakket", Sent = "Sendt", PickedUp = "Afhentet",
        Expired = "Udløbet", Cancelled = "Annulleret";

    /// <summary>Paid for - whatever has happened since.</summary>
    public static bool IsPaid(string status) => status is Paid or Packed or Sent or PickedUp;
}

public sealed record ShopOrder(string Status, string Line, int Kr, int PaidKr, string Delivery, string Email, string Tracking = "")
{
    public bool Paid => OrderStatuses.IsPaid(Status);
    public bool Expired => Status == OrderStatuses.Expired;
}

public sealed record ShopSearch(bool CanPay, List<ShopItem> Items);
public sealed record ShopItemPage(bool CanPay, ShopItem Item, List<ShopProof> Proof);
public sealed record ShopFeatured(ShopItem Item, List<ShopProof> Proof);
public sealed record ShopFront(bool CanPay, List<ShopFeatured> Items);
/// <summary>Køb nu for one thing (ItemIds null = the id in the path) or the whole basket.</summary>
public sealed record ShopBuy(string SiteBase, List<int>? ItemIds = null);
public sealed record ShopBuyAnswer(string? Url, string? Error);

// The marketplace (user 2026-10-20: "a place where they easily can see products in different categories").

/// <summary>A product type with how many things are for sale in it.</summary>
public sealed record ShopType(string Type, int Count);

/// <summary>Today's offers, as the buyer reads them: "Hver mandag: 50 % på bøger".</summary>
public sealed record ShopOffer(string Name, string Text, string When);

/// <summary>One page of the marketplace: the types (with counts), today's offers, and the things that match.</summary>
public sealed record ShopMarket(bool CanPay, List<ShopType> Types, List<ShopOffer> Today, List<ShopItem> Items, int Total);

/// <summary>A basket priced for today: each copy, what it costs today and why, and the total.</summary>
public sealed record ShopQuote(bool CanPay, List<ShopQuoteLine> Lines, int Total, int Saved, List<ShopOffer> Today, List<int> Gone);
public sealed record ShopQuoteLine(int Id, string Line, int ListKr, int Kr, string Offer);

/// <summary>Sort orders for the marketplace.</summary>
public static class ShopSorts
{
    public const string Newest = "nyeste", Cheapest = "billigst", Dearest = "dyrest", Title = "titel";
    public static readonly string[] All = [Newest, Cheapest, Dearest, Title];
}
