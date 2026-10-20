namespace ITMartinVarer.Contracts;

// The shop API (/api/butik/*) between Lager (the stock) and Hjem (bogshoppen.dk shows it). Key header X-Shop-Key.
// Never anything a buyer should not see: no boxes, notes, buyers or purchase prices.
public static class ShopHeaders
{
    public const string Key = "X-Shop-Key";
}

/// <summary>One thing in the shop. BuyNowKr null = no Køb nu yet (ExpectKr = what it is worth on the other sites,
/// minus 10 %). Available = copies not reserved by someone paying right now.</summary>
public sealed record ShopItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int? BuyNowKr, int? ExpectKr, int Available, bool HasPhoto);

/// <summary>The proof for the price promise: one listing on another site.</summary>
public sealed record ShopProof(string Source, int Kr, string Original, string Url, bool IsNew);

public static class OrderStatuses
{
    public const string Pending = "Afventer betaling", Paid = "Betalt", Expired = "Udløbet";
}

public sealed record ShopOrder(string Status, string Line, int Kr, int PaidKr, string Delivery, string Email)
{
    public bool Paid => Status == OrderStatuses.Paid;
    public bool Expired => Status == OrderStatuses.Expired;
}

public sealed record ShopSearch(bool CanPay, List<ShopItem> Items);
public sealed record ShopItemPage(bool CanPay, ShopItem Item, List<ShopProof> Proof);
public sealed record ShopFeatured(ShopItem Item, List<ShopProof> Proof);
public sealed record ShopFront(bool CanPay, List<ShopFeatured> Items);
public sealed record ShopBuy(string SiteBase);
public sealed record ShopBuyAnswer(string? Url, string? Error);
