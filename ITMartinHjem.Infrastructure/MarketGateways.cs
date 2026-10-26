using System.Net.Http.Json;
using ITMartinHjem.Application;
using ITMartinKunder.Client;
using ITMartinKunder.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Infrastructure;

// The marketplace for customers (2026-10-26): the kundenummer is checked in Kunder, the marks live in Bogshoppen Salg
// (same address and key as the shop, LagerShopRegistration).

public sealed class KunderMarketCodes(KunderClient kunder) : IMarketCodes
{
    public async Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, KunderRoles.Market, ct);
        return (r.Ok, r.Error);
    }
}

public sealed class SalgInterestGateway(HttpClient http) : IInterestGateway
{
    public async Task<ShopInterest> ToggleAsync(string customer, int itemId, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/butik/varer/{itemId}/interessant", new ShopInterestMark(customer), ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ShopInterest>(ct))!;
    }

    public async Task<List<int>> MineAsync(string customer, CancellationToken ct = default) =>
        (await http.GetFromJsonAsync<ShopMine>($"/api/butik/interesse/{Uri.EscapeDataString(customer)}", ct))?.ItemIds ?? [];
}

public static class MarketGatewayRegistration
{
    public static IServiceCollection AddMarketGateways(this IServiceCollection services, string? salgApi, string? shopKey)
    {
        services.AddScoped<IMarketCodes, KunderMarketCodes>();
        services.AddHttpClient<IInterestGateway, SalgInterestGateway>(c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(salgApi) ? "http://10.0.0.200:9154" : salgApi);
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.Add(ShopHeaders.Key, shopKey ?? "");
        });
        return services;
    }
}
