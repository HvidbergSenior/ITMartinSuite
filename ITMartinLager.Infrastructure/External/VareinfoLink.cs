using System.Net;
using System.Net.Http.Json;

namespace ITMartinLager.Infrastructure.External;

// Vareinfo's API for Lager (/api/lager/*), server to server on the LAN. The key is Lager__InfoKey here and
// Vareinfo__LagerKey there (CaptureHeaders.Key). Lager__VareinfoUrl = where to call (http://vareinfo-web:8080),
// Lager__VareinfoPublicUrl = the link a person clicks (https://vareinfo.bogshoppen.dk).
public sealed class VareinfoLink(HttpClient http, IConfiguration cfg) : IVareinfoGateway
{
    private string Base => (cfg["Lager:VareinfoUrl"] ?? "http://vareinfo-web:8080").TrimEnd('/');
    public string PublicBase => (cfg["Lager:VareinfoPublicUrl"] ?? "https://vareinfo.bogshoppen.dk").TrimEnd('/');

    private HttpRequestMessage Req(HttpMethod m, string path, object? body = null)
    {
        var req = new HttpRequestMessage(m, Base + path);
        req.Headers.Add(CaptureHeaders.Key, cfg["Lager:InfoKey"] ?? "");
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private static LagerException Down(HttpResponseMessage res) => new($"Vareinfo svarede ikke ({(int)res.StatusCode}). Prøv igen om lidt.");

    public async Task<CaptureBatch> NewAsync(int max, CancellationToken ct)
    {
        using var req = Req(HttpMethod.Get, $"/api/lager/nye?antal={max}");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw Down(res);
        return (await res.Content.ReadFromJsonAsync<CaptureBatch>(ct))!;
    }

    public async Task ReceivedAsync(CaptureReceived received, CancellationToken ct)
    {
        using var req = Req(HttpMethod.Post, "/api/lager/modtaget", received);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw Down(res);
    }

    public async Task<byte[]?> PhotoAsync(int id, int? tile, CancellationToken ct)
    {
        using var req = Req(HttpMethod.Get, $"/api/lager/foto/{id}" + (tile is { } t ? $"?del={t}" : ""));
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) throw Down(res);
        return await res.Content.ReadAsByteArrayAsync(ct);
    }
}
