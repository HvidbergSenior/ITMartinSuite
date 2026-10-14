using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Application;

// The shop on bogshoppen.dk (2026-10-15): the stock lives in Bogshoppen Lager (photoserver), this site only shows what
// Lager's shop API answers and sends the buyer to Stripe. Hjem keeps its own copies of the shapes - the two apps meet
// only as JSON.

/// <summary>BuyNowKr null = no Køb nu yet (ExpectKr = what it is worth on the other sites, minus 10 %).</summary>
public sealed record ShopItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int? BuyNowKr, int? ExpectKr, int Available, bool HasPhoto);

public sealed record ShopProof(string Source, int Kr, string Original, string Url, bool IsNew);

public sealed record ShopOrder(string Status, string Line, int Kr, int PaidKr, string Delivery, string Email)
{
    public bool Paid => Status == "Betalt";
    public bool Expired => Status == "Udløbet";
}

public interface IShopGateway
{
    Task<(bool CanPay, List<ShopItem> Items)> SearchAsync(string? text, string? kind, CancellationToken ct = default);
    /// <summary>Null = not for sale (sold, on auction, or never existed).</summary>
    Task<(bool CanPay, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default);
    Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default);
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
