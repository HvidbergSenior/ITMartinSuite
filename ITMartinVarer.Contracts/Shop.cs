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
/// ForShow = from a test box (user 2026-10-24): shown, never sold - no Køb nu, no kurv. Interested = how many customers
/// marked it "interessant" on the marketplace.</summary>
public sealed record ShopItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int? BuyNowKr, int? ExpectKr, int Available, bool HasPhoto, string Type = "", int? DayKr = null, string DayOffer = "", string Badge = "",
    string Usage = "", bool ForShow = false, int Interested = 0, ShopProof? Reference = null, string Deal = "");

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

/// <summary>A subgroup inside a type, from the data: a series ("Jonah Hex") or an artist/author, and how many.</summary>
public sealed record ShopGroup(string Name, int Count);

/// <summary>Today's offers, as the buyer reads them: "Hver mandag: 50 % på bøger".</summary>
public sealed record ShopOffer(string Name, string Text, string When);

/// <summary>One page of the marketplace: the types (with counts), today's offers, and the things that match.</summary>
public sealed record ShopMarket(bool CanPay, List<ShopType> Types, List<ShopOffer> Today, List<ShopItem> Items, int Total,
    List<ShopGroup>? Groups = null);

/// <summary>A basket priced for today: each copy, what it costs today and why, and the total.</summary>
public sealed record ShopQuote(bool CanPay, List<ShopQuoteLine> Lines, int Total, int Saved, List<ShopOffer> Today, List<int> Gone);
public sealed record ShopQuoteLine(int Id, string Line, int ListKr, int Kr, string Offer);

// "Interessant" (user 2026-10-24): a customer logged in with the kundenummer marks things - no buying yet.

/// <summary>Mark or unmark a thing for this kundenummer (the site checked it in Kunder first).</summary>
public sealed record ShopInterestMark(string Customer);
/// <summary>After pressing: whether it is marked now, how many have it marked, or the reason in Danish.</summary>
public sealed record ShopInterest(bool Marked, int Count, string? Error = null);
/// <summary>The things this kundenummer has marked.</summary>
public sealed record ShopMine(List<int> ItemIds);

/// <summary>Sort orders for the marketplace.</summary>
public static class ShopSorts
{
    public const string Newest = "nyeste", Cheapest = "billigst", Dearest = "dyrest", Title = "titel";
    public static readonly string[] All = [Newest, Cheapest, Dearest, Title];
}

/// <summary>One step of a deal: Count things for Kr kr in all.</summary>
public sealed record ShopDealStep(int Count, int Kr);

/// <summary>A deal for one group of kinds, with its steps: "Bøger: 2 for 50 · 5 for 100 kr".</summary>
public sealed record ShopDeal(string Group, IReadOnlyList<string> Kinds, IReadOnlyList<ShopDealStep> Steps)
{
    public string Name => $"{Group}: {string.Join(" · ", Steps.Select(s => $"{s.Count} for {s.Kr}"))} kr";
}

/// <summary>The deals as their own categories (user 2026-10-31, "lets say 50 and 100 kr. Books 2 for 50 5 for 100. Anders
/// and and basserne and similar 4 for 50 10 for 100. Dvd 4 for 50 10 for 100"): the things of these kinds without a price
/// or priced under CheapUnderKr, unless gold, silver or bronze. Which thing is in which deal is Lager's rule
/// (Shop.DealFor); a list shows one band: a deal's Name, Single (priced, no deal), NoPrice, or null (all).</summary>
public static class ShopDeals
{
    public const int CheapUnderKr = 10;
    public static readonly ShopDeal Books = new("Bøger", [Kinds.Bog], [new(2, 50), new(5, 100)]);
    public static readonly ShopDeal Blade = new("Blade", [Kinds.AndersAnd, Kinds.Tegneserie, Kinds.Superhelte], [new(4, 50), new(10, 100)]);
    public static readonly ShopDeal Dvds = new("DVD", [Kinds.Dvd], [new(4, 50), new(10, 100)]);
    /// <summary>User 2026-10-31 "VHS Bundles... for a price" - the price is a guess like DVD until the user says.</summary>
    public static readonly ShopDeal Vhs = new("VHS", [Kinds.Vhs], [new(4, 50), new(10, 100)]);
    public static readonly IReadOnlyList<ShopDeal> All = [Blade, Books, Dvds, Vhs];

    /// <summary>The deal for things of this kind, or null.</summary>
    public static ShopDeal? For(string kind) => All.FirstOrDefault(d => d.Kinds.Contains(kind));

    public static ShopDeal? Named(string? name) => All.FirstOrDefault(d => d.Name == name);

    public const string Single = "enkelt";
    /// <summary>Not priced and in no deal (user 2026-10-31: "No pricing yet. We are investigating"; unsure readings = a
    /// person looks - "low confidence should be manual investigation").</summary>
    public const string NoPrice = "uden-pris";
    public const string NoPriceName = "Ingen pris endnu";

    /// <summary>A band Lager knows, else null (= everything).</summary>
    public static string? Band(string? band) => band is Single or NoPrice || Named(band) is not null ? band : null;
}
