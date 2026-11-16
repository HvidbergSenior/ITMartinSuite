using System.Net.Http.Json;
using ITMartinVareinfo.Application;
using Microsoft.Extensions.Logging;

namespace ITMartinVareinfo.Infrastructure.External;

// Lager's Tjek answers (2026-11-10), server to server on the LAN: Vareinfo__LagerApi = where (http://lager-web:8080), the key
// is Vareinfo__LagerKey (= Lager__InfoKey there) in CaptureHeaders.Key - the same pair as Lager's calls the other way.
public sealed class LagerCheckAnswers(HttpClient http, IConfiguration cfg, ILogger<LagerCheckAnswers> log) : ICheckAnswers
{
    private string Base => (cfg["Vareinfo:LagerApi"] ?? "http://lager-web:8080").TrimEnd('/');

    private HttpRequestMessage Req(HttpMethod m, string boxCode, string path = "")
    {
        var req = new HttpRequestMessage(m, $"{Base}/api/vareinfo/tjek/{Uri.EscapeDataString(boxCode)}{path}");
        req.Headers.Add(CaptureHeaders.Key, cfg["Vareinfo:LagerKey"] ?? "");
        return req;
    }

    public async Task<CheckAnswers?> ForAsync(string boxCode, CancellationToken ct)
    {
        try
        {
            using var req = Req(HttpMethod.Get, boxCode);
            using var res = await http.SendAsync(req, ct);
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<CheckAnswers>(ct);
            log.LogWarning("Lager Tjek answered {Status}", (int)res.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { log.LogWarning("Lager Tjek: {Message}", ex.Message); }
        return null;
    }

    public async Task<bool> PressAsync(string boxCode, int itemId, string action, CancellationToken ct)
    {
        try
        {
            using var req = Req(HttpMethod.Post, boxCode, $"/varer/{itemId}/{action}");
            using var res = await http.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { return false; }
    }

    public async Task<byte[]?> PhotoAsync(string boxCode, int itemId, CancellationToken ct)
    {
        try
        {
            using var req = Req(HttpMethod.Get, boxCode, $"/varer/{itemId}/foto");
            using var res = await http.SendAsync(req, ct);
            return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { return null; }
    }
}
