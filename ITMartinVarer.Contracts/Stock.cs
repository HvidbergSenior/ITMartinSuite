namespace ITMartinVarer.Contracts;

// The stock API (/api/salg/*) between Lager (owns the things and their status) and Salg (owns orders, offers and
// payment) - 2026-10-21 split: "one app one responsible". Key header X-Salg-Key = Lager__SalgKey there, Salg__LagerKey
// here. Lager only answers with things that are for sale (in stock or til salg, copies left, a title); Salg decides
// the Køb nu price, today's offers and what is reserved, and tells Lager when a copy is sold.
public static class StockHeaders
{
    public const string Key = "X-Salg-Key";
}

/// <summary>One thing for sale, as Lager knows it. PriceKr = the price a person set; HintKr = the suggested shop price
/// (10 % under the other sites); Confidence = how sure the AI was what it is. Badge = "🏆 GULD" / "🥈 SØLV" / "🥉 BRONZE"
/// or "". Usage = Usages.</summary>
public sealed record StockItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int Quantity, string Usage, int? PriceKr, int? HintKr, double Confidence, bool HasPhoto, string Badge, bool ForShow = false);

/// <summary>One page of things for sale and how many match in all.</summary>
public sealed record StockPage(List<StockItem> Items, int Total);

/// <summary>One thing with the proof for the price promise (the cheapest exact listing per site).</summary>
public sealed record StockItemPage(StockItem Item, List<ShopProof> Proof);

public sealed record StockIds(List<int> Ids);

/// <summary>A paid sale: each line takes one copy out of the stock (the last one = Solgt). Ref = the order's token -
/// Lager books a Ref once, so sending the same sale again changes nothing.</summary>
public sealed record StockSale(string Ref, string Buyer, List<StockSaleLine> Lines);

/// <summary>How = what the item's note says, e.g. "i butikken for 120 kr (ordre 7)".</summary>
public sealed record StockSaleLine(int ItemId, int Kr, string How);
