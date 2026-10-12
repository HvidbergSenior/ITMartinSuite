using System.Net.Http.Json;

namespace ITMartinVareinfo.Infrastructure.External;

// Lager's API for Vareinfo (/api/info/*), server to server on the LAN. The key is Lager__InfoKey there and
// Vareinfo__LagerKey here. Vareinfo__LagerUrl = where to call (http://10.0.0.200:9149), Vareinfo__LagerPublicUrl =
// the link a person clicks (https://lager.bogshoppen.dk).
public sealed class LagerLink(HttpClient http, IConfiguration cfg) : ILagerGateway
{
    private string Base => (cfg["Vareinfo:LagerUrl"] ?? "http://lager-web:8080").TrimEnd('/');
    public string PublicBase => (cfg["Vareinfo:LagerPublicUrl"] ?? "https://lager.bogshoppen.dk").TrimEnd('/');

    private HttpRequestMessage Req(HttpMethod m, string path, object? body = null)
    {
        var req = new HttpRequestMessage(m, Base + path);
        req.Headers.Add("X-Info-Key", cfg["Vareinfo:LagerKey"] ?? "");
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        using (req)
        using (var res = await http.SendAsync(req, ct))
        {
            if (!res.IsSuccessStatusCode)
                throw new VareinfoException($"Lager svarede ikke ({(int)res.StatusCode}). Prøv igen om lidt.");
            return (await res.Content.ReadFromJsonAsync<T>(ct))!;
        }
    }

    private async Task SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using (req)
        using (var res = await http.SendAsync(req, ct))
            if (!res.IsSuccessStatusCode) throw new VareinfoException($"Lager svarede ikke ({(int)res.StatusCode}). Prøv igen om lidt.");
    }

    private sealed record Saved(int Added, int Merged);

    public async Task<(int Added, int Merged)> SaveAsync(int? photoId, IReadOnlyList<Found> found, IReadOnlyList<(int ItemId, int Quantity, string Barcode)> addTo, CancellationToken ct)
    {
        var r = await SendAsync<Saved>(Req(HttpMethod.Post, "/api/info/varer",
            new { PhotoId = photoId, Items = found, AddTo = addTo.Select(a => new { a.ItemId, a.Quantity, a.Barcode }) }), ct);
        return (r.Added, r.Merged);
    }

    public Task<List<InStock>> InStockAsync(IReadOnlyList<Found> found, CancellationToken ct) =>
        SendAsync<List<InStock>>(Req(HttpMethod.Post, "/api/info/findes", new { Items = found }), ct);

    private sealed record Wanted(int FromKr, List<KolofonWanted> Books);

    public async Task<(int FromKr, List<KolofonWanted> Books)> KolofonWantedAsync(CancellationToken ct)
    {
        var r = await SendAsync<Wanted>(Req(HttpMethod.Get, "/api/info/kolofon"), ct);
        return (r.FromKr, r.Books);
    }

    public Task KolofonTakenAsync(int itemId, CancellationToken ct) =>
        SendAsync(Req(HttpMethod.Post, $"/api/info/varer/{itemId}/kolofon/foto"), ct);

    public Task KolofonReadAsync(int itemId, KolofonReading? reading, CancellationToken ct) =>
        SendAsync(Req(HttpMethod.Post, $"/api/info/varer/{itemId}/kolofon", new { Reading = reading }), ct);
}
