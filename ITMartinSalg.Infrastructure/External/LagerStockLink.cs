using System.Net;
using System.Net.Http.Json;

namespace ITMartinSalg.Infrastructure.External;

// Lager's stock API for Salg (/api/salg/*), server to server on the docker network. The key is Salg__LagerKey here and
// Lager__SalgKey there (StockHeaders.Key). Salg__LagerUrl = where to call (http://lager-web:8080), Salg__LagerPublicUrl
// = the link a person clicks (https://lager.bogshoppen.dk).
public sealed class LagerStockLink(HttpClient http, IConfiguration cfg) : IStockGateway
{
    private string Base => (cfg["Salg:LagerUrl"] ?? "http://lager-web:8080").TrimEnd('/');
    public string PublicBase => (cfg["Salg:LagerPublicUrl"] ?? "https://lager.bogshoppen.dk").TrimEnd('/');

    private static string E(string? s) => Uri.EscapeDataString(s ?? "");

    public async Task<StockPage> ListedAsync(IReadOnlyCollection<string>? kinds, string? usage, string text, string sort, int skip, int take, CancellationToken ct)
    {
        var art = string.Concat((kinds ?? []).Select(k => "&art=" + E(k)));
        return await GetAsync<StockPage>($"/api/salg/varer?q={E(text)}&brugt={E(usage)}&sort={E(sort)}&fra={skip}&antal={take}{art}", ct) ?? new StockPage([], 0);
    }

    public async Task<Dictionary<string, int>> PerKindAsync(CancellationToken ct) =>
        await GetAsync<Dictionary<string, int>>("/api/salg/arter", ct) ?? [];

    public Task<StockItemPage?> ItemAsync(int id, CancellationToken ct) => GetAsync<StockItemPage>($"/api/salg/varer/{id}", ct);

    public async Task<List<StockItem>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        using var res = await SendAsync(HttpMethod.Post, "/api/salg/varer", new StockIds(ids.ToList()), ct);
        return await res.Content.ReadFromJsonAsync<List<StockItem>>(ct) ?? [];
    }

    public async Task<List<StockItemPage>> FeaturedAsync(int max, CancellationToken ct) =>
        await GetAsync<List<StockItemPage>>($"/api/salg/udvalgte?antal={max}", ct) ?? [];

    public async Task<byte[]?> PhotoAsync(int id, CancellationToken ct)
    {
        using var res = await SendAsync(HttpMethod.Get, $"/api/salg/varer/{id}/foto", null, ct, notFoundOk: true);
        return res.StatusCode == HttpStatusCode.NotFound ? null : await res.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task BookAsync(StockSale sale, CancellationToken ct)
    {
        using var _ = await SendAsync(HttpMethod.Post, "/api/salg/solgt", sale, ct);
    }

    // Null on 404 (not for sale).
    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var res = await SendAsync(HttpMethod.Get, path, null, ct, notFoundOk: true);
        return res.StatusCode == HttpStatusCode.NotFound ? null : await res.Content.ReadFromJsonAsync<T>(ct);
    }

    // Every way Lager can fail (down, slow, refused) becomes one SalgException - the shop and the order job handle that.
    private async Task<HttpResponseMessage> SendAsync(HttpMethod m, string path, object? body, CancellationToken ct, bool notFoundOk = false)
    {
        using var req = new HttpRequestMessage(m, Base + path);
        req.Headers.Add(StockHeaders.Key, cfg["Salg:LagerKey"] ?? "");
        if (body is not null) req.Content = JsonContent.Create(body);
        HttpResponseMessage res;
        try { res = await http.SendAsync(req, ct); }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new SalgException("Lageret svarer ikke lige nu. Prøv igen om lidt.");
        }
        if (res.IsSuccessStatusCode || (notFoundOk && res.StatusCode == HttpStatusCode.NotFound)) return res;
        var code = (int)res.StatusCode;
        res.Dispose();
        throw new SalgException($"Lageret svarede ikke ({code}). Prøv igen om lidt.");
    }
}
