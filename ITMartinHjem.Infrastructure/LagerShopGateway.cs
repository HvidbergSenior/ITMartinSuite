using System.Net;
using System.Net.Http.Json;
using ITMartinHjem.Application;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Infrastructure;

// Bogshoppen Lager's shop API (/api/butik/*) on the photoserver, from the NAS over the LAN. Key: ShopHeaders.Key =
// Hjem__LagerShopKey here and Lager__ShopKey there. Hjem__LagerApi = http://10.0.0.200:9149.
public sealed class LagerShopGateway(HttpClient http) : IShopGateway
{
    public async Task<(bool CanPay, List<ShopItem> Items)> SearchAsync(string? text, string? kind, CancellationToken ct = default)
    {
        var b = await http.GetFromJsonAsync<ShopSearch>($"/api/butik/soeg?q={Uri.EscapeDataString(text ?? "")}&art={Uri.EscapeDataString(kind ?? "")}", ct);
        return (b?.CanPay ?? false, b?.Items ?? []);
    }

    public async Task<(bool CanPay, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/butik/varer/{id}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var b = (await res.Content.ReadFromJsonAsync<ShopItemPage>(ct))!;
        return (b.CanPay, b.Item, b.Proof);
    }

    public async Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(CancellationToken ct = default)
    {
        var b = await http.GetFromJsonAsync<ShopFront>("/api/butik/forside", ct);
        return (b?.Items ?? []).Select(x => (x.Item, x.Proof)).ToList();
    }

    public async Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/butik/varer/{id}/foto", ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync(ct) : null;
    }

    public async Task<(string? Url, string? Error)> BuyAsync(int id, string siteBase, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/butik/varer/{id}/koeb", new ShopBuy(siteBase), ct);
        if (res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.BadRequest)
        {
            var a = await res.Content.ReadFromJsonAsync<ShopBuyAnswer>(ct);
            return (a?.Url, a?.Url is null ? a?.Error ?? "Det gik ikke." : null);
        }
        res.EnsureSuccessStatusCode();
        return (null, null);
    }

    public async Task<ShopOrder?> OrderAsync(string token, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/butik/ordrer/{Uri.EscapeDataString(token)}", ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<ShopOrder>(ct);
    }

    public async Task<ShopMarket> MarketAsync(string? type, string? kind, string? text, string? sort, int page, CancellationToken ct = default)
    {
        static string E(string? s) => Uri.EscapeDataString(s ?? "");
        return (await http.GetFromJsonAsync<ShopMarket>($"/api/butik/marked?type={E(type)}&art={E(kind)}&q={E(text)}&sort={E(sort)}&side={page}", ct))!;
    }

    private sealed record BasketIds(IReadOnlyList<int> ItemIds);

    public async Task<ShopQuote> QuoteAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync("/api/butik/kurv", new BasketIds(itemIds), ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ShopQuote>(ct))!;
    }

    public async Task<(string? Url, string? Error)> BuyBasketAsync(IReadOnlyList<int> itemIds, string siteBase, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync("/api/butik/kurv/koeb", new ShopBuy(siteBase, itemIds.ToList()), ct);
        if (res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.BadRequest)
        {
            var a = await res.Content.ReadFromJsonAsync<ShopBuyAnswer>(ct);
            return (a?.Url, a?.Url is null ? a?.Error ?? "Det gik ikke." : null);
        }
        res.EnsureSuccessStatusCode();
        return (null, null);
    }
}

public static class LagerShopRegistration
{
    public static IServiceCollection AddLagerShopGateway(this IServiceCollection services, string? lagerApi, string? shopKey)
    {
        services.AddHttpClient<IShopGateway, LagerShopGateway>(c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(lagerApi) ? "http://10.0.0.200:9149" : lagerApi);
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.Add(ShopHeaders.Key, shopKey ?? "");
        });
        return services;
    }
}
