using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinKunder.Infrastructure;

// Snak's POST /api/rum with the shared key (Kunder__SnakKey here = Snak__ApiKey in Snak, both in magic.env, 16+ characters).
// Kunder__SnakUrl: where Kunder reaches Snak (default the public address).
internal sealed class HttpSnakRooms(HttpClient http, IConfiguration cfg, ILogger<HttpSnakRooms> log) : ISnakRooms
{
    private string Url => (cfg["Kunder:SnakUrl"] ?? "https://snak.itmartin.dk").TrimEnd('/');
    private string Key => cfg["Kunder:SnakKey"] ?? "";

    private sealed record Answer(string? Id, string? Titel, string? Link, string? Fejl);

    public async Task<(string? Link, string? Error)> CreateAsync(string title, string customerRef, CancellationToken ct)
    {
        if (Key.Length < 16) return (null, "Snak-nøglen er ikke sat (Kunder__SnakKey og Snak__ApiKey i magic.env).");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{Url}/api/rum")
            {
                Content = JsonContent.Create(new { titel = title, kunde = customerRef }),
            };
            req.Headers.Add("X-Snak-Key", Key);
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Snak kender ikke nøglen. Tjek at Kunder__SnakKey og Snak__ApiKey er ens.");
            var a = await res.Content.ReadFromJsonAsync<Answer>(ct);
            if (res.IsSuccessStatusCode && a?.Link is { Length: > 0 } link) return (link, null);
            log.LogWarning("Snak said {Status} to a new room", (int)res.StatusCode);
            return (null, a?.Fejl ?? "Snak lavede ikke rummet. Prøv igen om lidt.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            log.LogWarning(ex, "Snak not reached");
            return (null, "Snak svarer ikke lige nu. Prøv igen om lidt.");
        }
    }
}
