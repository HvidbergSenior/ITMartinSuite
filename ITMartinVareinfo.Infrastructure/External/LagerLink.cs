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

    public Task<List<LagerBox>> RecentBoxesAsync(int count, CancellationToken ct) =>
        SendAsync<List<LagerBox>>(Req(HttpMethod.Get, $"/api/info/kasser?antal={count}"), ct);

    public async Task<LagerBox?> BoxAsync(string code, CancellationToken ct)
    {
        using var req = Req(HttpMethod.Get, "/api/info/kasser/" + Uri.EscapeDataString(code));
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) throw new VareinfoException($"Lager svarede ikke ({(int)res.StatusCode}). Prøv igen om lidt.");
        return await res.Content.ReadFromJsonAsync<LagerBox>(ct);
    }

    public Task<LagerBox> NewBoxAsync(string place, CancellationToken ct) =>
        SendAsync<LagerBox>(Req(HttpMethod.Post, "/api/info/kasser", new { Place = place }), ct);

    private sealed record Saved(int Added, int Merged);

    public async Task<(int Added, int Merged)> SaveAsync(string boxCode, int? photoId, IReadOnlyList<Found> found, CancellationToken ct)
    {
        var r = await SendAsync<Saved>(Req(HttpMethod.Post, $"/api/info/kasser/{Uri.EscapeDataString(boxCode)}/varer",
            new { PhotoId = photoId, Items = found }), ct);
        return (r.Added, r.Merged);
    }

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
