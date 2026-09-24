using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinStrategi.Server.Data.Entities;

namespace ITMartinStrategi.Server.Services;

public sealed record GuideDraft(string Title, string Trigger, string Body, List<string> Sources);

/// <summary>The two AI jobs: draft a guide (with web search, so it matches the
/// current patch) and answer a question from the game's own guides. One call per
/// button press and a hard daily cap - never a loop over guides.</summary>
public sealed class StrategiAi(IConfiguration config, ILogger<StrategiAi> logger)
{
    private const string ModelId = "claude-opus-5";
    private readonly int _maxPerDay = config.GetValue("Strategi:MaxAiCallsPerDay", 40);
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private const string Style =
        "Du skriver korte, praktiske strategiguides på dansk til en erfaren spiller, der ikke vil snyde, " +
        "men gerne vil forstå spillet og spille bedre. Skriv konkret: hvad gør man, hvornår og hvorfor. " +
        "Brug spillets egne navne på ting (det engelske navn i parentes, hvis den danske oversættelse er uklar). " +
        "Ingen indledning og ingen afrunding. Format: '## ' for mellemoverskrifter, '- ' for punkter, " +
        "'**fed**' for det vigtigste. Højst ca. 350 ord.";

    public int Used { get { lock (_lock) { Roll(); return _used; } } }
    public int Max => _maxPerDay;

    public async Task<GuideDraft> DraftAsync(Game game, string section, string topic, IReadOnlyList<Guide> existing, CancellationToken ct = default)
    {
        Take();
        var mine = existing.Where(g => g.Section == Sections.Mine).Select(g => $"- {g.Title}: {Trim(g.Body, 600)}").ToList();
        var titles = existing.Where(g => g.Section != Sections.Mine).Select(g => $"- {Sections.Label(g.Section)}: {g.Title}").ToList();

        var prompt = new StringBuilder()
            .AppendLine($"Spil: {game.Name} ({game.Platform}, {game.Version}).")
            .AppendLine($"Afsnit: {Sections.Label(section)} – {Sections.Hint(section)}")
            .AppendLine($"Emne: {topic}")
            .AppendLine()
            .AppendLine("Søg først på nettet, så guiden passer til den nuværende version af spillet (patch-noter, wiki, " +
                        "strategiguides fra erfarne spillere). Brug ikke forældede regler.")
            .AppendLine()
            .AppendLine("Guides der allerede findes (gentag dem ikke, henvis hellere):")
            .AppendLine(titles.Count > 0 ? string.Join('\n', titles) : "- ingen endnu")
            .AppendLine()
            .AppendLine("Spillerens egne strategier (byg videre på dem, modsig dem kun med en god grund):")
            .AppendLine(mine.Count > 0 ? string.Join('\n', mine) : "- ingen endnu")
            .AppendLine()
            .AppendLine("Svar til sidst med KUN ét JSON-objekt: {\"title\": \"kort titel\", \"body\": \"guiden i det aftalte format\"}")
            .ToString();

        var res = await Client().Messages.Create(new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 8000,
            System = Style,
            OutputConfig = new OutputConfig { Effort = Effort.Medium },
            Tools = [new ToolUnion(new WebSearchTool20260209 { MaxUses = 5 })],
            Messages = [new() { Role = Role.User, Content = prompt }],
        }, ct);
        LogUsage("draft", res, res.Usage.ServerToolUse?.WebSearchRequests ?? 0);
        if (res.StopReason == "refusal") throw new InvalidOperationException("AI'en ville ikke skrive den guide.");

        var sources = new List<string>();
        foreach (var block in res.Content)
            if (block.TryPickWebSearchToolResult(out var wr) && wr.Content.TryPickWebSearchResultBlocks(out var list))
                sources.AddRange(list.Select(r => r.Url));

        var text = string.Concat(res.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
        var raw = ParseJson<RawDraft>(text);
        if (string.IsNullOrWhiteSpace(raw.Body)) throw new InvalidOperationException("AI'en gav et tomt udkast. Prøv igen.");
        return new GuideDraft(
            string.IsNullOrWhiteSpace(raw.Title) ? topic : raw.Title.Trim(),
            // The chip/topic the player picked is the cleanest label for the choice.
            section == Sections.Valg ? topic.Trim() : "",
            raw.Body.Trim(),
            sources.Distinct().Take(6).ToList());
    }

    public async Task<string> AskAsync(Game game, string question, IReadOnlyList<Guide> guides, CancellationToken ct = default)
    {
        Take();
        var context = new StringBuilder();
        foreach (var g in guides.OrderByDescending(g => g.Section == Sections.Mine))
        {
            var block = $"### {Sections.Label(g.Section)}: {g.Title}{(g.Trigger.Length > 0 ? $" (valg: {g.Trigger})" : "")}\n{g.Body}\n\n";
            if (context.Length + block.Length > 60_000) break;
            context.Append(block);
        }
        var res = await Client().Messages.Create(new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 4000,
            System = Style + " Svar på spørgsmålet. Brug spillerens egne guides nedenfor først – især 'Mine strategier' – " +
                     "og din egen viden om spillet til resten. Sig det, hvis du er usikker på noget i den nuværende version.",
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            Messages = [new() { Role = Role.User, Content =
                $"Spil: {game.Name} ({game.Version}).\n\nSpillerens guides:\n{(context.Length > 0 ? context.ToString() : "(ingen endnu)")}\n\nSpørgsmål: {question}" }],
        }, ct);
        LogUsage("ask", res, 0);
        if (res.StopReason == "refusal") throw new InvalidOperationException("AI'en ville ikke svare på det.");
        return string.Concat(res.Content.Select(b => b.TryPickText(out var t) ? t.Text : "")).Trim();
    }

    private AnthropicClient Client() =>
        new() { ApiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Mangler Claude-nøgle på serveren.") };

    private void LogUsage(string what, Message res, long searches)
    {
        // Claude Opus 5 list price: $5 / $25 per million tokens, web search $10 per 1,000.
        var cost = res.Usage.InputTokens * 5e-6 + res.Usage.OutputTokens * 25e-6 + searches * 0.01;
        logger.LogInformation("AI {What}: {In} in / {Out} out tokens, {Searches} searches, ~${Cost:0.000}",
            what, res.Usage.InputTokens, res.Usage.OutputTokens, searches, cost);
    }

    private void Take()
    {
        lock (_lock)
        {
            Roll();
            if (_used >= _maxPerDay) throw new InvalidOperationException("Dagens AI-kvote er brugt op. Prøv igen i morgen.");
            _used++;
        }
    }

    private void Roll()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _day) { _day = today; _used = 0; }
    }

    private static T ParseJson<T>(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new InvalidOperationException("AI'en svarede ikke i det aftalte format. Prøv igen.");
        return JsonSerializer.Deserialize<T>(text[start..(end + 1)], Json) ?? throw new InvalidOperationException("Tomt svar.");
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private sealed class RawDraft
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
    }
}
