using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;
using Microsoft.Extensions.Configuration;

namespace ITMartinIndkoeb.Infrastructure;

/// <summary>Uploaded pictures on disk: Indkoeb__DataDir/billeder/{family id}/{random}.jpg. Never served to the web.</summary>
internal sealed class FilBilledLager(string root) : IBilledLager
{
    public async Task<string> GemAsync(string husstandId, Stream data, string filnavn, CancellationToken ct = default)
    {
        var dir = Path.Combine(root, Safe(husstandId));
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(filnavn).ToLowerInvariant() is { Length: > 1 and <= 5 } e ? e : ".jpg";
        var fil = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30] + ext;
        await using var fs = File.Create(Path.Combine(dir, fil));
        await data.CopyToAsync(fs, ct);
        return fil;
    }

    public Stream? Aabn(string husstandId, string fil)
    {
        var p = Path.Combine(root, Safe(husstandId), Safe(fil));
        return File.Exists(p) ? File.OpenRead(p) : null;
    }

    public void Slet(string husstandId, string fil)
    {
        try { File.Delete(Path.Combine(root, Safe(husstandId), Safe(fil))); } catch (IOException) { }
    }

    private static string Safe(string s) => Path.GetFileName(s);
}

/// <summary>Kvittering's POST /api/laes - the same Indkoeb__KvitteringUrl + key as the receipt button.</summary>
internal sealed class BilledLaeserKlient(HttpClient http, IConfiguration cfg) : IBilledLaeser
{
    private string Url => (cfg["Indkoeb:KvitteringUrl"] ?? "").TrimEnd('/');
    private string Key => cfg["Indkoeb:KvitteringKey"] ?? "";
    public bool Klar => Url.Length > 0 && Key.Length >= 16;

    public async Task<BilledLaesning> LaesAsync(Stream billede, string filnavn, Sted? sted, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fil = new StreamContent(billede);
        fil.Headers.ContentType = new MediaTypeHeaderValue(Path.GetExtension(filnavn).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg");
        form.Add(fil, "billede", Path.GetFileName(filnavn));
        if (sted is not null)
        {
            form.Add(new StringContent(sted.Lat.ToString(CultureInfo.InvariantCulture)), "lat");
            form.Add(new StringContent(sted.Lng.ToString(CultureInfo.InvariantCulture)), "lng");
        }
        using var req = new HttpRequestMessage(HttpMethod.Post, Url + "/api/laes") { Content = form };
        req.Headers.Add("X-Kvittering-Key", Key);
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return Fejl("Kvittering svarede ikke rigtigt. Prøver igen.");
            var s = await res.Content.ReadFromJsonAsync<Svar>(cancellationToken: ct);
            if (s is null) return Fejl("Tomt svar fra Kvittering.");
            return new BilledLaesning(s.Slags ?? "ukendt", s.Butik, s.Total, s.Dato is { } d ? DateOnly.FromDateTime(d) : null, s.Tjek ?? [],
                s.Tilbud?.Butik, s.Tilbud?.Vare, s.Tilbud?.Pris, s.Tilbud?.KunMedApp ?? false, s.Tilbud?.App, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return Fejl("Kvittering svarer ikke lige nu. Prøver igen.");
        }
    }

    private static BilledLaesning Fejl(string t) => new("ukendt", null, null, null, [], null, null, null, false, null, t);

    private sealed record Svar(string? Slags, string? Butik, decimal? Total, DateTime? Dato, List<string>? Tjek, TilbudSvar? Tilbud);
    private sealed record TilbudSvar(string? Butik, string? Vare, decimal? Pris, bool KunMedApp, string? App);
}
