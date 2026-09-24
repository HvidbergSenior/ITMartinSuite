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
        "You write short, practical strategy guides in English for an experienced player who does not want to cheat, " +
        "but wants to understand the game and play better. Be concrete: what to do, when and why. " +
        "Use the game's own names for things. " +
        "Ingen indledning og ingen afrunding. Format: '## ' for mellemoverskrifter, '- ' for punkter, " +
        "'**bold**' for the most important. At most about 350 words.";

    // Paid Claude API calls are off unless Strategi:AiEnabled=true - Martin does
    // not want the app to cost money; guides are written in Claude Code instead.
    public bool Enabled { get; } = config.GetValue("Strategi:AiEnabled", false);

    public int Used { get { lock (_lock) { Roll(); return _used; } } }
    public int Max => _maxPerDay;

    public async Task<GuideDraft> DraftAsync(Game game, string section, string topic, IReadOnlyList<Guide> existing, CancellationToken ct = default)
    {
        Take();
        var mine = existing.Where(g => g.Section == Sections.Mine).Select(g => $"- {g.Title}: {Trim(g.Body, 600)}").ToList();
        var titles = existing.Where(g => g.Section != Sections.Mine).Select(g => $"- {Sections.Label(g.Section)}: {g.Title}").ToList();

        var prompt = new StringBuilder()
            .AppendLine($"Game: {game.Name} ({game.Platform}, {game.Version}).")
            .AppendLine($"Section: {Sections.Label(section)} – {Sections.Hint(section)}")
            .AppendLine($"Topic: {topic}")
            .AppendLine()
            .AppendLine("Search the web first so the guide fits the current version of the game (patch notes, wiki, " +
                        "strategy guides by experienced players). Do not use outdated rules.")
            .AppendLine()
            .AppendLine("Guides that already exist (do not repeat them, refer to them instead):")
            .AppendLine(titles.Count > 0 ? string.Join('\n', titles) : "- none yet")
            .AppendLine()
            .AppendLine("The player's own strategies (build on them, contradict them only for a good reason):")
            .AppendLine(mine.Count > 0 ? string.Join('\n', mine) : "- none yet")
            .AppendLine()
            .AppendLine("Finish with ONLY one JSON object: {\"title\": \"short title\", \"body\": \"the guide in the agreed format\"}")
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
        if (string.IsNullOrWhiteSpace(raw.Body)) throw new InvalidOperationException("The AI returned an empty draft. Try again.");
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
            System = Style + " Answer the question. Use the player's own guides below first – especially 'My strategies' – " +
                     "and your own knowledge of the game for the rest. Say so if you are unsure about something in the current version.",
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            Messages = [new() { Role = Role.User, Content =
                $"Game: {game.Name} ({game.Version}).\n\nThe player's guides:\n{(context.Length > 0 ? context.ToString() : "(none yet)")}\n\nQuestion: {question}" }],
        }, ct);
        LogUsage("ask", res, 0);
        if (res.StopReason == "refusal") throw new InvalidOperationException("The AI would not answer that.");
        return string.Concat(res.Content.Select(b => b.TryPickText(out var t) ? t.Text : "")).Trim();
    }

    private AnthropicClient Client() =>
        new() { ApiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Missing Claude key on the server.") };

    private void LogUsage(string what, Message res, long searches)
    {
        // Claude Opus 5 list price: $5 / $25 per million tokens, web search $10 per 1,000.
        var cost = res.Usage.InputTokens * 5e-6 + res.Usage.OutputTokens * 25e-6 + searches * 0.01;
        logger.LogInformation("AI {What}: {In} in / {Out} out tokens, {Searches} searches, ~${Cost:0.000}",
            what, res.Usage.InputTokens, res.Usage.OutputTokens, searches, cost);
    }

    private void Take()
    {
        if (!Enabled) throw new InvalidOperationException("AI is turned off in the app.");
        lock (_lock)
        {
            Roll();
            if (_used >= _maxPerDay) throw new InvalidOperationException("Today's AI quota is used up. Try again tomorrow.");
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
        if (start < 0 || end <= start) throw new InvalidOperationException("The AI did not answer in the agreed format. Try again.");
        return JsonSerializer.Deserialize<T>(text[start..(end + 1)], Json) ?? throw new InvalidOperationException("Tomt svar.");
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private sealed class RawDraft
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
    }
}
