using System.Net.Http.Json;
using ITMartinVarer.Contracts;
using ITMartinVurdering.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinVurdering.Infrastructure;

// Lager's /api/vurdering with Vurdering__LagerKey (= Lager__VurderingKey there). Vurdering__LagerUrl = http://lager-web:8080 on the photoserver.
internal sealed class HttpLagerReview(HttpClient http, ILogger<HttpLagerReview> log) : ILagerReview
{
    public async Task<VurderingQueue?> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, string usage, CancellationToken ct)
    {
        try { return await http.GetFromJsonAsync<VurderingQueue>($"/api/vurdering/koe?bunke={pile}&antal={take}&spring={string.Join(',', skip)}&brug={Uri.EscapeDataString(usage)}", ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { log.LogWarning(e, "Lager did not answer the queue"); return null; }
    }

    public async Task<string?> SetPriceAsync(int itemId, int kr, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsJsonAsync($"/api/vurdering/varer/{itemId}/pris", new VurderingPrice(kr), ct);
            return res.IsSuccessStatusCode ? null : (await res.Content.ReadAsStringAsync(ct)).Trim('"') is { Length: > 0 } why ? why : "Lager gemte ikke prisen.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        { log.LogWarning(e, "Lager did not save the price"); return "Lager svarer ikke lige nu – prøv igen om lidt."; }
    }

    public async Task<string?> SetUsageAsync(int itemId, string usage, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsJsonAsync($"/api/vurdering/varer/{itemId}/brug", new VurderingUsage(usage), ct);
            return res.IsSuccessStatusCode ? null : (await res.Content.ReadAsStringAsync(ct)).Trim('"') is { Length: > 0 } why ? why : "Lager gemte det ikke.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        { log.LogWarning(e, "Lager did not save the usage"); return "Lager svarer ikke lige nu – prøv igen om lidt."; }
    }

    public async Task<byte[]?> PhotoAsync(int itemId, CancellationToken ct)
    {
        try { using var res = await http.GetAsync($"/api/vurdering/varer/{itemId}/foto", ct); return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync(ct) : null; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }
}

public static class VurderingInfrastructure
{
    public static IServiceCollection AddVurderingInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddHttpClient<ILagerReview, HttpLagerReview>(c =>
        {
            c.BaseAddress = new Uri(cfg["Vurdering:LagerUrl"] ?? "http://lager-web:8080");
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.Add(VurderingHeaders.Key, cfg["Vurdering:LagerKey"] ?? "");
        });
        return services;
    }
}
