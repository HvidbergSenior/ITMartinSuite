using System.Net.Http.Json;
using ITMartinVarer.Contracts;
using ITMartinVurdering.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinVurdering.Infrastructure;

// Lager's /api/vurdering with Vurdering__LagerKey (= Lager__VurderingKey there). Vurdering__LagerUrl = http://lager-web:8080 on the photoserver.
internal sealed class HttpLagerReview(HttpClient http, ILogger<HttpLagerReview> log) : ILagerReview, ILagerPriceLists
{
    public async Task<List<VurderingGold>?> GoldAsync(CancellationToken ct)
    {
        try { return await http.GetFromJsonAsync<List<VurderingGold>>("/api/vurdering/guld", ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { log.LogWarning(e, "Lager did not answer the gold list"); return null; }
    }

    public async Task<List<VurderingKiosk>?> KioskAsync(CancellationToken ct)
    {
        try { return await http.GetFromJsonAsync<List<VurderingKiosk>>("/api/vurdering/kiosk", ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { log.LogWarning(e, "Lager did not answer the kiosk prices"); return null; }
    }

    public async Task<string?> SaveKioskAsync(VurderingKioskSave price, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsJsonAsync("/api/vurdering/kiosk", price, ct);
            return res.IsSuccessStatusCode ? null : (await res.Content.ReadAsStringAsync(ct)).Trim('"') is { Length: > 0 } why ? why : "Lager gemte ikke prisen.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return "Lager svarer ikke lige nu – prøv igen om lidt."; }
    }

    public async Task<bool> DeleteKioskAsync(int id, CancellationToken ct)
    {
        try { using var res = await http.DeleteAsync($"/api/vurdering/kiosk/{id}", ct); return res.IsSuccessStatusCode; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
    }

    public async Task<VurderingQueue?> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, string usage, CancellationToken ct)
    {
        try { return await http.GetFromJsonAsync<VurderingQueue>($"/api/vurdering/koe?bunke={pile}&antal={take}&spring={string.Join(',', skip)}&brug={Uri.EscapeDataString(usage)}", ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { log.LogWarning(e, "Lager did not answer the queue"); return null; }
    }

    public async Task<VurderingQueue?> SearchAsync(string text, IReadOnlyCollection<int> skip, CancellationToken ct)
    {
        try { return await http.GetFromJsonAsync<VurderingQueue>($"/api/vurdering/soeg?q={Uri.EscapeDataString(text)}&spring={string.Join(',', skip)}", ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { log.LogWarning(e, "Lager did not answer the search"); return null; }
    }

    public async Task<int?> FindPricesAsync(int itemId, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsync($"/api/vurdering/varer/{itemId}/find", null, ct);
            return res.IsSuccessStatusCode && await res.Content.ReadFromJsonAsync<VurderingHunted>(ct) is { } h ? h.Sites : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { log.LogWarning(e, "Lager did not hunt the prices"); return null; }
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

    public Task<string?> SetRareAsync(int itemId, bool rare, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/sjaelden", new VurderingRare(rare), "Lager gemte ikke mærket.", ct);

    public Task<string?> StraightenAsync(int itemId, int degrees, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/skaev", new VurderingRotate(degrees), "Lager kunne ikke rette billedet op.", ct);

    public Task<string?> CropOwnAsync(int itemId, PhotoArea keep, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/beskaer", keep, "Lager kunne ikke beskære billedet.", ct);

    public Task<string?> RotateAsync(int itemId, int degrees, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/drej", new VurderingRotate(degrees), "Lager kunne ikke dreje billedet.", ct);

    public async Task<string?> DiscardAsync(int itemId, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsync($"/api/vurdering/varer/{itemId}/kasser", null, ct);
            return res.IsSuccessStatusCode && await res.Content.ReadFromJsonAsync<VurderingDiscarded>(ct) is { } d ? d.Was : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { log.LogWarning(e, "Lager did not discard {Id}", itemId); return null; }
    }

    public Task<string?> UndoDiscardAsync(int itemId, string was, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/kasser/fortryd", new VurderingDiscarded(was), "Lager kunne ikke hente den tilbage.", ct);

    public Task<string?> RecutAsync(int itemId, PhotoArea area, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/udsnit", area, "Lager kunne ikke lave det nye billede.", ct);

    public async Task<byte[]?> PileAsync(int itemId, CancellationToken ct)
    {
        try
        {
            using var res = await http.GetAsync($"/api/vurdering/varer/{itemId}/bunkefoto", ct);
            return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { log.LogWarning(e, "Lager did not send the pile photo"); return null; }
    }

    public Task<string?> SetPhotoHiddenAsync(int itemId, bool hidden, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/skjulfoto", new VurderingPhotoHidden(hidden), "Lager gemte det ikke.", ct);

    public Task<string?> CoverMarkAsync(int itemId, PhotoArea area, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/maerke", area, "Lager kunne ikke rette billedet.", ct);

    public Task<string?> UncoverAsync(int itemId, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/maerke/fortryd", new { }, "Lager kunne ikke hente billedet tilbage.", ct);

    public Task<string?> SetNoteAsync(int itemId, string note, CancellationToken ct) =>
        PostAsync($"/api/vurdering/varer/{itemId}/note", new VurderingNote(note), "Lager gemte ikke kommentaren.", ct);

    // POST to Lager: null = saved, else Lager's reason (or the fallback, or "Lager svarer ikke").
    private async Task<string?> PostAsync<T>(string url, T body, string fallback, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsJsonAsync(url, body, ct);
            return res.IsSuccessStatusCode ? null : (await res.Content.ReadAsStringAsync(ct)).Trim('"') is { Length: > 0 } why ? why : fallback;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        { log.LogWarning(e, "Lager did not answer {Url}", url); return "Lager svarer ikke lige nu – prøv igen om lidt."; }
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

    public async Task<List<VurderingDescribe>?> WaitingDescriptionsAsync(int take, CancellationToken ct)
    {
        try { return await http.GetFromJsonAsync<List<VurderingDescribe>>($"/api/vurdering/beskriv?antal={take}", ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { log.LogWarning(e, "Lager did not answer the descriptions"); return null; }
    }

    public async Task<bool> SetDescriptionAsync(int itemId, VurderingDescription d, CancellationToken ct)
    {
        try { using var res = await http.PostAsJsonAsync($"/api/vurdering/varer/{itemId}/beskrivelse", d, ct); return res.IsSuccessStatusCode; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
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
        services.AddTransient<ILagerPriceLists>(sp => (HttpLagerReview)sp.GetRequiredService<ILagerReview>());
        services.AddHttpClient<ILagerReview, HttpLagerReview>(c =>
        {
            c.BaseAddress = new Uri(cfg["Vurdering:LagerUrl"] ?? "http://lager-web:8080");
            c.Timeout = TimeSpan.FromSeconds(120);   // "Find priser nu" asks every site - up to a minute or so
            c.DefaultRequestHeaders.Add(VurderingHeaders.Key, cfg["Vurdering:LagerKey"] ?? "");
        });
        services.AddSingleton<ISagStore, JsonSagStore>();
        services.AddSingleton<IThingValuer, ClaudeThingValuer>();
        return services;
    }
}
