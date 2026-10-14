using System.Net;
using System.Net.Http.Json;
using ITMartinHjem.Application;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Infrastructure;

// Bogshoppen Lager's shop API (/api/butik/*) on the photoserver, from the NAS over the LAN. Key: X-Shop-Key =
// Hjem__LagerShopKey here and Lager__ShopKey there. Hjem__LagerApi = http://10.0.0.200:9149.
public sealed class LagerShopGateway(HttpClient http) : IShopGateway
{
    private sealed record SearchBody(bool CanPay, List<ShopItem> Items);
    private sealed record ItemBody(bool CanPay, ShopItem Item, List<ShopProof> Proof);
    private sealed record BuyAnswer(string? Url, string? Error);

    public async Task<(bool CanPay, List<ShopItem> Items)> SearchAsync(string? text, string? kind, CancellationToken ct = default)
    {
        var b = await http.GetFromJsonAsync<SearchBody>($"/api/butik/soeg?q={Uri.EscapeDataString(text ?? "")}&art={Uri.EscapeDataString(kind ?? "")}", ct);
        return (b?.CanPay ?? false, b?.Items ?? []);
    }

    public async Task<(bool CanPay, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/butik/varer/{id}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var b = (await res.Content.ReadFromJsonAsync<ItemBody>(ct))!;
        return (b.CanPay, b.Item, b.Proof);
    }

    public async Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/butik/varer/{id}/foto", ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync(ct) : null;
    }

    public async Task<(string? Url, string? Error)> BuyAsync(int id, string siteBase, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/butik/varer/{id}/koeb", new { SiteBase = siteBase }, ct);
        if (res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.BadRequest)
        {
            var a = await res.Content.ReadFromJsonAsync<BuyAnswer>(ct);
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
}

public static class LagerShopRegistration
{
    public static IServiceCollection AddLagerShopGateway(this IServiceCollection services, string? lagerApi, string? shopKey)
    {
        services.AddHttpClient<IShopGateway, LagerShopGateway>(c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(lagerApi) ? "http://10.0.0.200:9149" : lagerApi);
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.Add("X-Shop-Key", shopKey ?? "");
        });
        return services;
    }
}
