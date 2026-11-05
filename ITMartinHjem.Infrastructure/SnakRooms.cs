using System.Net.Http.Json;
using ITMartinHjem.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinHjem.Infrastructure;

// Snak's POST /api/rum with the shared key (Hjem__SnakKey here = Snak__ApiKey in Snak, 16+ characters).
// Hjem runs on the NAS and reaches Snak on the photoserver over the LAN (Hjem__SnakUrl, default http://10.0.0.200:9151).
internal sealed class HttpSnakRooms(HttpClient http, string key, ILogger<HttpSnakRooms> log) : ISnakRooms
{
    private sealed record Answer(string? Id, string? Titel, string? Link, string? Fejl);

    public async Task<string?> CreateAsync(string title, string name, string firstMessage, CancellationToken ct = default)
    {
        if (key.Length < 16) { log.LogWarning("Hjem__SnakKey is not set - Skriv til mig falls back to the old chat"); return null; }
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/rum")
            {
                Content = JsonContent.Create(new { titel = title, kunde = "itmartin.dk", navn = name, besked = firstMessage }),
            };
            req.Headers.Add("X-Snak-Key", key);
            using var res = await http.SendAsync(req, ct);
            var a = res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<Answer>(ct) : null;
            if (a?.Link is { Length: > 0 } link) return link;
            log.LogWarning("Snak said {Status} to a new room from Skriv til mig", (int)res.StatusCode);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            log.LogWarning(ex, "Snak not reached from Skriv til mig");
            return null;
        }
    }
}

public static class SnakRoomsRegistration
{
    public static IServiceCollection AddSnakRooms(this IServiceCollection services, string? snakUrl, string? snakKey)
    {
        services.AddHttpClient(nameof(HttpSnakRooms), c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(snakUrl) ? "http://10.0.0.200:9151" : snakUrl);
            c.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<ISnakRooms>(sp => new HttpSnakRooms(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpSnakRooms)),
            snakKey ?? "", sp.GetRequiredService<ILogger<HttpSnakRooms>>()));
        services.AddSingleton<SnakStartUseCases>();
        return services;
    }
}
