using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Application;

// The shop on bogshoppen.dk (2026-10-15): the stock lives in Bogshoppen Lager (photoserver), this site only shows what
// Lager's shop API answers and sends the buyer to Stripe. The two apps meet only as JSON, in the shapes of
// ITMartinVarer.Contracts (ShopItem, ShopProof, ShopOrder).

public interface IShopGateway
{
    Task<(bool CanPay, List<ShopItem> Items)> SearchAsync(string? text, string? kind, CancellationToken ct = default);
    /// <summary>Null = not for sale (sold, on auction, or never existed).</summary>
    Task<(bool CanPay, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default);
    Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default);
    /// <summary>The items chosen in Lager for the front pages, each with its proof links.</summary>
    Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(CancellationToken ct = default);
    /// <returns>Stripe's payment page, or the reason in Danish.</returns>
    Task<(string? Url, string? Error)> BuyAsync(int id, string siteBase, CancellationToken ct = default);
    Task<ShopOrder?> OrderAsync(string token, CancellationToken ct = default);
    /// <summary>The marketplace: product types with counts, today's offers, one page of things.</summary>
    Task<ShopMarket> MarketAsync(string? type, string? kind, string? usage, string? text, string? sort, int page, CancellationToken ct = default);
    /// <summary>The basket priced with today's offers.</summary>
    Task<ShopQuote> QuoteAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default);
    /// <returns>Stripe's payment page for the whole basket, or the reason in Danish.</returns>
    Task<(string? Url, string? Error)> BuyBasketAsync(IReadOnlyList<int> itemIds, string siteBase, CancellationToken ct = default);
}

/// <summary>Who sells (Hjem__ShopCompany / ShopCvr / ShopAddress / ShopEmail / ShopPhone). The law wants it shown
/// before a sale, so Køb nu is hidden until company and CVR are set.</summary>
public sealed record ShopSeller(string Company, string Cvr, string Address, string Email, string Phone)
{
    public bool Complete => Company != "" && Cvr != "";
}

public sealed class ShopUseCases(IShopGateway lager, ShopSeller seller)
{
    public const string Unreachable = "Lageret svarer ikke lige nu – prøv igen om lidt.";

    public ShopSeller Seller => seller;

    public async Task<(bool CanBuy, List<ShopItem> Items, string? Error)> SearchAsync(string? text, string? kind, CancellationToken ct = default)
    {
        try
        {
            var (canPay, items) = await lager.SearchAsync(text, kind, ct);
            return (canPay && seller.Complete, items, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (false, [], Unreachable); }
    }

    public async Task<(bool CanBuy, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default)
    {
        try { return await lager.ItemAsync(id, ct) is { } x ? (x.CanPay && seller.Complete, x.Item, x.Proof) : null; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    /// <summary>"Fundet i kasserne" - empty when Lager does not answer (the front page must never break).</summary>
    public async Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(CancellationToken ct = default)
    {
        try { return await lager.FeaturedAsync(ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return []; }
    }

    public async Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default)
    {
        try { return await lager.PhotoAsync(id, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    public async Task<(string? Url, string? Error)> BuyAsync(int id, string siteBase, CancellationToken ct = default)
    {
        if (!seller.Complete) return (null, "Køb nu er ikke åbnet endnu.");
        try { return await lager.BuyAsync(id, siteBase, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (null, Unreachable); }
    }

    public async Task<ShopOrder?> OrderAsync(string token, CancellationToken ct = default)
    {
        try { return await lager.OrderAsync(token, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    /// <summary>The marketplace (user 2026-10-20: "a place where they easily can see products in different categories").
    /// Køb nu only when the seller is complete - the law wants the seller shown before a sale.</summary>
    public async Task<(ShopMarket? Market, string? Error)> MarketAsync(string? type, string? kind, string? usage, string? text, string? sort, int page, CancellationToken ct = default)
    {
        try
        {
            var m = await lager.MarketAsync(type, kind, usage, text, sort, page, ct);
            return (m with { CanPay = m.CanPay && seller.Complete }, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (null, Unreachable); }
    }

    /// <summary>The basket at today's prices. Null = Lager does not answer.</summary>
    public async Task<ShopQuote?> QuoteAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default)
    {
        if (itemIds.Count == 0) return new ShopQuote(false, [], 0, 0, [], []);
        try
        {
            var q = await lager.QuoteAsync(itemIds, ct);
            return q with { CanPay = q.CanPay && seller.Complete };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    public async Task<(string? Url, string? Error)> BuyBasketAsync(IReadOnlyList<int> itemIds, string siteBase, CancellationToken ct = default)
    {
        if (!seller.Complete) return (null, "Køb nu er ikke åbnet endnu.");
        if (itemIds.Count == 0) return (null, "Kurven er tom.");
        try { return await lager.BuyBasketAsync(itemIds, siteBase, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (null, Unreachable); }
    }
}

/// <summary>Who sees the shop (user 2026-10-21: "nothing should be on homepages. i want to test it first"): everyone when
/// Hjem__ShowFound=true, else only a browser that opened /vis-butik?kode=Hjem__ShopPreviewCode (cookie bs_preview).</summary>
public sealed record ShopVisibility(bool ShowAll, string PreviewCode)
{
    public const string Cookie = "bs_preview";

    /// <summary>A code shorter than 8 never opens anything.</summary>
    public bool CodeOk(string? code) => PreviewCode.Length >= 8 && code == PreviewCode;

    public bool Visible(string? cookie) => ShowAll || CodeOk(cookie);
}

public static class ShopRegistration
{
    public static IServiceCollection AddHjemShop(this IServiceCollection services, ShopSeller seller)
    {
        services.AddSingleton(seller);
        services.AddScoped<ShopUseCases>();
        return services;
    }
}
