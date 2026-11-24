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
    // Newest cheap model (2026-11-24, user wanted a better summary; same price class).
    private const string Model = "claude-haiku-5-5";

    public async Task<string> SummarizeAsync(NewsItem item, CancellationToken ct)
    {
        var apiKey = cfg["CLAUDE:APIKEY"] ?? "";
        if (apiKey.Length == 0) return "";

        var hasDesc = !string.IsNullOrWhiteSpace(item.Description);
        var prompt = $"""
            Skriv et fyldigt dansk resumé (150-200 ord) af denne nyhedsartikel.
            Dæk: hvad skete, hvem er involveret, hvorfor det skete, hvad sker der nu.
            Rolig, neutral journalistisk tone. Ingen kildehenvisninger.
            {(!hasDesc ? "Kun titlen er tilgængelig. Skriv 2-3 rolige sætninger om, hvad titlen fortæller – find ikke på detaljer." : "Brug kun det, der står i titlen og beskrivelsen – find ikke på detaljer.")}

            Titel: {item.Title}
            Kilde: {item.SourceName}
            {(hasDesc ? $"Beskrivelse: {item.Description}" : "")}

            Svar KUN med resuméteksten — ingen overskrift, ingen forklaring.
            """;
        // Thinking OFF (2026-11-24): with it on, Haiku 5.5 spent all 600 tokens thinking and wrote no text (stop_reason max_tokens).
        var body = JsonSerializer.Serialize(new
        {
            model = Model, max_tokens = 600, thinking = new { type = "disabled" },
            messages = new[] { new { role = "user", content = prompt } },
        });

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
            if (!res.IsSuccessStatusCode) { log.LogWarning("Claude summary {Status}: {Body}", res.StatusCode, Cut(raw)); return ""; }
            var text = TextOf(raw);
            if (text.Length == 0) log.LogWarning("Claude summary without text: {Body}", Cut(raw));
            return text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Claude summary failed");
            return "";
        }
    }

    private const string Blank = "\n\n";

    // The answer is a list of blocks; only "text" blocks are the summary (2026-11-24: content[0] was not always text -> no summary).
    public static string TextOf(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return "";
            return string.Join(Blank, content.EnumerateArray()
                .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text" && b.TryGetProperty("text", out _))
                .Select(b => b.GetProperty("text").GetString()?.Trim() ?? "")
                .Where(t => t.Length > 0));
        }
        catch (JsonException) { return ""; }
    }

    private static string Cut(string s) => s.Length > 300 ? s[..300] : s;
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
