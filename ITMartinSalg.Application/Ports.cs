using ITMartinSalg.Domain;

namespace ITMartinSalg.Application;

// What the use cases need from the outside world. Implemented in ITMartinSalg.Infrastructure.

/// <summary>Lager (the stock), server to server: what is for sale, and booking a paid sale. Throws SalgException when
/// Lager does not answer.</summary>
public interface IStockGateway
{
    /// <summary>Where a person opens Lager (https://lager.bogshoppen.dk) - the Ordrer page links to the items there.</summary>
    string PublicBase { get; }
    /// <summary>Things for sale of these kinds (null = all), new or used (null = both), every word in the search text,
    /// sorted (ShopSorts), one page.</summary>
    Task<StockPage> ListedAsync(IReadOnlyCollection<string>? kinds, string? usage, string text, string sort, int skip, int take, CancellationToken ct);
    /// <summary>Things for sale per kind.</summary>
    Task<Dictionary<string, int>> PerKindAsync(CancellationToken ct);
    /// <summary>Null = not for sale (sold, on an auction, gone).</summary>
    Task<StockItemPage?> ItemAsync(int id, CancellationToken ct);
    /// <summary>The ones of these that are for sale.</summary>
    Task<List<StockItem>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    /// <summary>"Fundet i kasserne": the things a person chose for the front page, with their proof.</summary>
    Task<List<StockItemPage>> FeaturedAsync(int max, CancellationToken ct);
    Task<byte[]?> PhotoAsync(int id, CancellationToken ct);
    /// <summary>A paid sale: the copies leave the stock. The same sale twice changes nothing.</summary>
    Task BookAsync(StockSale sale, CancellationToken ct);
}

/// <summary>Stripe Checkout (hosted page: card, MobilePay - whatever the Stripe account has switched on).</summary>
public interface IPaymentGateway
{
    bool Enabled { get; }
    /// <summary>Starts a checkout for one order. Returns Stripe's id and the page to send the buyer to.</summary>
    Task<(string CheckoutId, string Url)> StartAsync(Order order, string description, string successUrl, string cancelUrl,
        int shippingKr, string pickup, CancellationToken ct);
    Task<PaymentState> StateAsync(string checkoutId, CancellationToken ct);
}

public interface IOrderRepository
{
    /// <summary>With its lines.</summary>
    Task AddAsync(Order order, CancellationToken ct);
    Task UpdateAsync(Order order, CancellationToken ct);
    Task<Order?> GetAsync(int id, CancellationToken ct);
    Task<Order?> ByTokenAsync(string token, CancellationToken ct);
    /// <summary>Waiting for payment, or paid while Lager does not know yet.</summary>
    Task<List<Order>> OpenAsync(CancellationToken ct);
    /// <summary>Copies reserved per item right now (lines of pending, not expired orders).</summary>
    Task<Dictionary<int, int>> ReservedAsync(IReadOnlyCollection<int> itemIds, DateTime utcNow, CancellationToken ct);
    Task<List<Order>> RecentAsync(int max, CancellationToken ct);
}

/// <summary>Kunder, the Kundekartotek for all apps (2026-10-21): a paid order makes the buyer a customer there - as a
/// buyer, NOT as a yes to offers. Never throws: what Kunder does not take now waits in an outbox.</summary>
public interface ICustomerRecord
{
    Task BoughtAsync(Order order, CancellationToken ct);
}

/// <summary>The day offers (Salg's Tilbud page).</summary>
public interface ICampaignRepository
{
    Task<List<Campaign>> AllAsync(CancellationToken ct);
    /// <summary>Adds (Id 0) or updates.</summary>
    Task SaveAsync(Campaign campaign, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>The "interessant" marks from the marketplace.</summary>
public interface IInterestRepository
{
    Task<Interest?> FindAsync(string customer, int itemId, CancellationToken ct);
    Task AddAsync(Interest interest, CancellationToken ct);
    Task RemoveAsync(Interest interest, CancellationToken ct);
    /// <summary>Marks per item (items without marks are left out).</summary>
    Task<Dictionary<int, int>> CountsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);
    Task<List<int>> MineAsync(string customer, CancellationToken ct);
    /// <summary>The most marked items: count, and when the last mark came.</summary>
    Task<List<(int ItemId, int Count, DateTime LastAt)>> TopAsync(int max, CancellationToken ct);
}

/// <summary>Settings the use cases read (Salg__* in magic.env).</summary>
public sealed record SalgOptions
{
    /// <summary>The key bogshoppen.dk sends (Salg__ShopKey) - its calls to /api/butik/* need it.</summary>
    public string ShopKey { get; init; } = "";
    /// <summary>Suggested prices from here up need a person's price before Køb nu (Salg__ShopAutoMaxKr).</summary>
    public int ShopAutoMaxKr { get; init; } = Shop.DefaultAutoMaxKr;
    /// <summary>Shipping with PostNord, kr (Salg__ShopShippingKr). 0 = pickup only.</summary>
    public int ShopShippingKr { get; init; }
    /// <summary>Where and how to pick up (Salg__ShopPickup), shown at checkout. Empty = no pickup.</summary>
    public string ShopPickup { get; init; } = "";
}

/// <summary>A message for the person, in Danish - shown as it is.</summary>
public sealed class SalgException(string message) : Exception(message);
