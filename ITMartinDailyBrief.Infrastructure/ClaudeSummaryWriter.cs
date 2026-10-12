using System.Text;
using System.Text.Json;
using ITMartinDailyBrief.Application;
using ITMartinDailyBrief.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinDailyBrief.Infrastructure;

// One article -> 150-200 words of calm Danish, with Haiku (cheap). No key = no summaries ("").
public sealed class ClaudeSummaryWriter(HttpClient http, IConfiguration cfg, ILogger<ClaudeSummaryWriter> log) : ISummaryWriter
{
    private const string Model = "claude-haiku-4-5-20251001";

    public async Task<string> SummarizeAsync(NewsItem item, CancellationToken ct)
    {
        var apiKey = cfg["CLAUDE:APIKEY"] ?? "";
        if (apiKey.Length == 0) return "";

        var hasDesc = !string.IsNullOrWhiteSpace(item.Description);
        var prompt = $"""
            Skriv et fyldigt dansk resumé (150-200 ord) af denne nyhedsartikel.
            Dæk: hvad skete, hvem er involveret, hvorfor det skete, hvad sker der nu.
            Rolig, neutral journalistisk tone. Ingen kildehenvisninger.
            {(!hasDesc ? "Kun titlen er tilgængelig — brug din viden om denne nyhed til at skrive et fyldigt resumé." : "")}

            Titel: {item.Title}
            Kilde: {item.SourceName}
            {(hasDesc ? $"Beskrivelse: {item.Description}" : "")}

            Svar KUN med resuméteksten — ingen overskrift, ingen forklaring.
            """;
        var body = JsonSerializer.Serialize(new { model = Model, max_tokens = 600, messages = new[] { new { role = "user", content = prompt } } });

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            using var res = await http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) { log.LogWarning("Claude summary {Status}", res.StatusCode); return ""; }
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()?.Trim() ?? "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Claude summary failed");
            return "";
        }
    }
}

public static class DailyBriefInfrastructure
{
    public static IServiceCollection AddDailyBriefInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IFeedReader, RssFeedReader>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Nyheder/1.0 (+https://itmartin.dk)");
        });
        services.AddHttpClient<ISummaryWriter, ClaudeSummaryWriter>(c => c.Timeout = TimeSpan.FromSeconds(25));
        services.AddSingleton(new SummaryBudget(int.TryParse(cfg["News:AiPerDay"], out var n) && n >= 0 ? n : 200));
        services.AddSingleton<NewsFeed>();
        services.AddSingleton<Summaries>();
        return services;
    }
}
