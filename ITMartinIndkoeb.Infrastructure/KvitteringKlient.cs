using System.Net.Http.Headers;
using System.Net.Http.Json;
using ITMartinIndkoeb.Application;
using Microsoft.Extensions.Configuration;

namespace ITMartinIndkoeb.Infrastructure;

/// <summary>Kvittering's POST /api/scan. Indkoeb__KvitteringUrl (e.g. http://10.0.0.200:9084) + Indkoeb__KvitteringKey (= Receipt__ApiKey).</summary>
internal sealed class KvitteringKlient(HttpClient http, IConfiguration cfg) : IKvitteringLaeser
{
    private string Url => (cfg["Indkoeb:KvitteringUrl"] ?? "").TrimEnd('/');
    private string Key => cfg["Indkoeb:KvitteringKey"] ?? "";
    public bool Klar => Url.Length > 0 && Key.Length >= 16;

    public async Task<KvitteringForslag> LaesAsync(Stream billede, string filnavn, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fil = new StreamContent(billede);
        fil.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fil, "billede", string.IsNullOrWhiteSpace(filnavn) ? "kvittering.jpg" : Path.GetFileName(filnavn));
        using var req = new HttpRequestMessage(HttpMethod.Post, Url + "/api/scan") { Content = form };
        req.Headers.Add("X-Kvittering-Key", Key);
        try
        {
            using var res = await http.SendAsync(req, ct);
            var svar = await res.Content.ReadFromJsonAsync<Svar>(cancellationToken: ct);
            if (svar is null) return new(null, null, null, null, "Kvitteringen kunne ikke læses lige nu. Skriv beløbet selv.");
            if (!svar.Success || svar.TotalAmount is not > 0)
                return new(null, null, null, svar.MerchantName, svar.FailureReason ?? "Totalen kunne ikke læses. Skriv beløbet selv.");
            return new(svar.TotalAmount, null, svar.PurchaseDate is { } d ? DateOnly.FromDateTime(d) : null, svar.MerchantName, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return new(null, null, null, null, "Kvitteringen kunne ikke læses lige nu. Skriv beløbet selv.");
        }
    }

    private sealed record Svar(bool Success, string? FailureReason, string? MerchantName, DateTime? PurchaseDate, decimal? TotalAmount);
}
