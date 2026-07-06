using System.Text;
using System.Text.Json.Nodes;
using ITMartinLive.Server.Models;

namespace ITMartinLive.Server.Services;

public class SummaryService(IHttpClientFactory http, IConfiguration config, ILogger<SummaryService> logger)
{
    private readonly string? _apiKey = config["CLAUDE__APIKEY"] ?? config["Claude:ApiKey"];

    public async Task<string?> GenerateAsync(LiveEvent ev)
    {
        if (_apiKey is null) return null;

        var lines = ev.Updates
            .Where(u => u.Type != UpdateType.Summary)
            .OrderBy(u => u.CreatedAt)
            .Select(u => $"[{u.CreatedAt:HH:mm}] {u.Type}: {u.Text}");

        var prompt = $"""
            Du er sportsjournalist. Lav en kort, fængende opsummering på dansk af denne live-dækning.

            Begivenhed: {ev.SportEmoji} {ev.Name}
            Resultat/status: {ev.HeaderText}

            Opdateringer:
            {string.Join("\n", lines)}

            Skriv 5-8 punkter med • foran hvert punkt. Fremhæv de vigtigste øjeblikke. Vær kortfattet og levende.
            """;

        try
        {
            var client = http.CreateClient("claude");
            var body = new
            {
                model      = "claude-haiku-4-5-20251001",
                max_tokens = 600,
                messages   = new[] { new { role = "user", content = prompt } }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
            return json?["content"]?[0]?["text"]?.GetValue<string>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Summary generation failed");
            return null;
        }
    }
}
