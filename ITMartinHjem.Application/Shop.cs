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
