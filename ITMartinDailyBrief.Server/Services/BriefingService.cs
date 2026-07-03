using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITMartinDailyBrief.Server.Models;

namespace ITMartinDailyBrief.Server.Services;

public class BriefingService
{
    private readonly IConfiguration        _config;
    private readonly ILogger<BriefingService> _log;

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private Briefing?  _cached;
    private DateTime   _cachedAt;
    private string     _cachedFilter = "";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    public BriefingService(IConfiguration config, ILogger<BriefingService> log)
    {
        _config = config;
        _log    = log;
    }

    public Briefing? LastBriefing    => _cached;

    private Briefing? _current;
    public  Briefing? CurrentBriefing => _current;
    public  void      SetCurrentBriefing(Briefing b) => _current = b;

    public async Task<Briefing> GetBriefingAsync(
        List<FeedItem> items, string topicFilter = "")
    {
        var filterChanged = topicFilter != _cachedFilter;
        if (!filterChanged && _cached is not null &&
            DateTime.UtcNow - _cachedAt < CacheTtl)
            return _cached;

        var apiKey = _config["CLAUDE:APIKEY"] ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            _log.LogWarning("No Claude API key configured — returning raw briefing");
            return BuildFallbackBriefing(items);
        }

        try
        {
            var briefing = await CallClaudeAsync(items, topicFilter, apiKey);
            _cached       = briefing;
            _cachedAt     = DateTime.UtcNow;
            _cachedFilter = topicFilter;
            return briefing;
        }
        catch (Exception ex)
        {
            _log.LogError("Claude briefing failed: {Msg}", ex.Message);
            var fallback = _cached ?? BuildFallbackBriefing(items);
            _cached   = fallback;
            _cachedAt = DateTime.UtcNow;
            return fallback;
        }
    }

    // ── On-demand single-article summary ────────────────

    public async Task<string> GetArticleSummaryAsync(FeedItem item)
    {
        var apiKey = _config["CLAUDE:APIKEY"] ?? "";
        if (string.IsNullOrEmpty(apiKey)) return "";

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

        try
        {
            var body = new
            {
                model      = "claude-haiku-4-5-20251001",
                max_tokens = 600,
                messages   = new[] { new { role = "user", content = prompt } },
            };

            var req = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post,
                "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new System.Net.Http.StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                System.Text.Encoding.UTF8, "application/json");

            var resp = await Http.SendAsync(req);
            var raw  = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return "";

            var doc = JsonDocument.Parse(raw);
            return doc.RootElement.GetProperty("content")[0]
                      .GetProperty("text").GetString() ?? "";
        }
        catch { return ""; }
    }

    // ── Claude call ──────────────────────────────────────

    private async Task<Briefing> CallClaudeAsync(
        List<FeedItem> items, string topicFilter, string apiKey)
    {
        var articleList = items.Take(30).Select((a, i) => new
        {
            id          = i,
            title       = a.Title,
            source      = a.SourceName,
            source_color = a.SourceColor,
            url         = a.Url,
            published   = a.Published.ToString("o"),
            description = a.Description,
        });

        var filterInstruction = string.IsNullOrWhiteSpace(topicFilter)
            ? "Include all stories."
            : $"User topic filter: \"{topicFilter}\". Only include stories matching this interest. Skip unrelated stories entirely.";

        var systemPrompt = $$"""
            Du er nyhedsredaktør for DailyBrief, en dansk anti-doomscroll nyhedsapp.
            Brugerne besøger aldrig de originale nyhedssider. Din tekst ER artiklen — det eneste de læser.
            Skriv ALT på dansk — resumé, vinkel, overblik og "hvorfor det betyder noget".

            Din opgave:
            1. Gruppér artikler om samme nyhed i én samlet historie.
            2. Skriv et fyldig, velstruktureret resumé for hver historie (150-250 ord på dansk).
               Syntesér ALLE kilder til ét sammenhængende narrativ. Dæk: hvad skete, hvem er involveret,
               hvorfor skete det, hvad sker der nu. Skriv i rolig, neutral journalistisk tone.
               Sig IKKE "ifølge" eller nævn kildenavne i resuméet — fortæl bare historien.
            3. For hver kilde: ét sætning om den specifikke vinkel eller detalje den kilde tilføjer (på dansk).
            4. Skriv én sætning om "hvorfor det betyder noget" (på dansk).
            5. Forældethed: hvis den nyeste artikel er ældre end 48 timer, sæt staleness til fx "2 dage gammel"
               eller "Opdatering af gammel nyhed". Ellers null.
            6. Skriv et dagligt overblik på 2 sætninger der dækker alle historier samlet (på dansk).
            7. {{filterInstruction}}

            Return ONLY valid JSON — no markdown, no explanation:
            {
              "stories": [
                {
                  "headline": "string",
                  "summary": "string",
                  "why_it_matters": "string",
                  "staleness": null,
                  "sources": [
                    {
                      "name": "string",
                      "color": "string (hex from input)",
                      "angle": "string",
                      "url": "string",
                      "published": "ISO8601"
                    }
                  ]
                }
              ],
              "digest": "string"
            }
            """;

        var body = new
        {
            model      = "claude-haiku-4-5-20251001",
            max_tokens = 4000,
            system     = systemPrompt,
            messages   = new[]
            {
                new { role = "user", content = JsonSerializer.Serialize(articleList, JsonOpts) }
            },
        };

        var req = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new System.Net.Http.StringContent(
            JsonSerializer.Serialize(body, JsonOpts),
            Encoding.UTF8, "application/json");

        var resp = await Http.SendAsync(req);
        var raw  = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Claude API {resp.StatusCode}: {raw}");

        var envelope = JsonDocument.Parse(raw);
        var text     = envelope.RootElement
            .GetProperty("content")[0]
            .GetProperty("text").GetString() ?? "";

        return ParseBriefing(StripFences(text));
    }

    // ── Parsing ──────────────────────────────────────────

    private static Briefing ParseBriefing(string json)
    {
        var dto = JsonSerializer.Deserialize<BriefingDto>(json, JsonOpts)
                  ?? throw new Exception("Null briefing response");

        return new Briefing
        {
            Digest  = dto.Digest ?? "",
            Stories = (dto.Stories ?? []).Select(s => new Story
            {
                Headline     = s.Headline     ?? "",
                Summary      = s.Summary      ?? "",
                WhyItMatters = s.WhyItMatters ?? "",
                Staleness    = string.IsNullOrWhiteSpace(s.Staleness) ? null : s.Staleness,
                Sources      = (s.Sources ?? []).Select(src => new StorySource
                {
                    Name      = src.Name  ?? "",
                    Color     = src.Color ?? "#6B7280",
                    Angle     = src.Angle ?? "",
                    Url       = src.Url   ?? "",
                    Published = DateTime.TryParse(src.Published, out var d) ? d : DateTime.UtcNow,
                }).ToList(),
            }).ToList(),
        };
    }

    // ── Fallback (no API key) ────────────────────────────

    private static Briefing BuildFallbackBriefing(List<FeedItem> items) => new()
    {
        Digest  = "",
        Stories = items.Take(10).Select(a => new Story
        {
            Headline     = a.Title,
            Summary      = a.Description,
            WhyItMatters = "",
            Sources      =
            [
                new StorySource
                {
                    Name      = a.SourceName,
                    Color     = a.SourceColor,
                    Url       = a.Url,
                    Published = a.Published,
                }
            ],
        }).ToList(),
    };

    // ── Helpers ──────────────────────────────────────────

    private static string StripFences(string s)
    {
        s = s.Trim();
        if (s.StartsWith("```")) s = s[(s.IndexOf('\n') + 1)..];
        if (s.EndsWith("```"))   s = s[..s.LastIndexOf("```")];
        return s.Trim();
    }

    // ── DTOs ─────────────────────────────────────────────

    private record BriefingDto(
        [property: JsonPropertyName("stories")] List<StoryDto>? Stories,
        [property: JsonPropertyName("digest")]  string?         Digest);

    private record StoryDto(
        [property: JsonPropertyName("headline")]       string?          Headline,
        [property: JsonPropertyName("summary")]        string?          Summary,
        [property: JsonPropertyName("why_it_matters")] string?          WhyItMatters,
        [property: JsonPropertyName("staleness")]      string?          Staleness,
        [property: JsonPropertyName("sources")]        List<SourceDto>? Sources);

    private record SourceDto(
        [property: JsonPropertyName("name")]      string? Name,
        [property: JsonPropertyName("color")]     string? Color,
        [property: JsonPropertyName("angle")]     string? Angle,
        [property: JsonPropertyName("url")]       string? Url,
        [property: JsonPropertyName("published")] string? Published);
}
