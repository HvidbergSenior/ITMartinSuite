using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinLaes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLaes.Infrastructure.External;

// One call per part of ~20 pages (never per page - CLAUDE.md cost rules), with the cheap model; one call for the summary
// with a stronger one. Every call is counted in the database (AiDays) and refused over Laes:MaxAiCallsPerDay, so a loop
// or a restart cannot run up the bill. The book's own cap is in SummaryPlan.
public sealed class ClaudeBookSummarizer(IConfiguration cfg, IDbContextFactory<LaesDb> dbf, ILogger<ClaudeBookSummarizer> log) : IBookSummarizer
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim CapGate = new(1, 1);

    private string PartModel => cfg["Laes:PartModel"] ?? "claude-haiku-5-5";
    private string SummaryModel => cfg["Laes:SummaryModel"] ?? "claude-sonnet-5-5";
    public int MaxCallsPerDay => int.TryParse(cfg["Laes:MaxAiCallsPerDay"], out var n) ? n : 120;
    public bool Enabled => !string.IsNullOrWhiteSpace(cfg["Claude:ApiKey"]);

    public async Task<int> CallsTodayAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return (await db.AiDays.FindAsync([DateOnly.FromDateTime(DateTime.Now)], ct))?.Calls ?? 0;
    }

    private const string PartSystem = """
        Du hjælper en dansk læser med at forstå en bog. Du får teksten fra nogle sider af bogen; hver side starter med
        [Side N]. Skriv korte noter på DANSK om det, siderne siger: de vigtigste idéer, begreber, eksempler og
        konklusioner - 5-12 sætninger, egne ord, ingen lange citater. Nævn sidetal ved de vigtigste idéer, fx "(s. 37)".
        Find også de kapitler/hovedafsnit, der STARTER på disse sider (den store overskrift og sidetallet). Ingen
        underoverskrifter, ingen indholdsfortegnelse. Starter der intet kapitel her, så en tom liste.
        Er siderne kun forside, kolofon, indhold eller register, så sig det kort.
        Svar ved at kalde værktøjet report_part.
        """;

    private const string SummarySystem = """
        Du skriver et resumé på DANSK af en bog ud fra noter om alle dens dele (noterne kom i rækkefølge, med sidetal).
        Skriv:
        - summary: et resumé af hele bogen på 150-250 ord - hvad den handler om, hovedtanken og hvad læseren får ud af den.
        - points: de 5-10 vigtigste pointer, hver én klar sætning.
        - chapters: bogens kapitler i rækkefølge med titel, startside og 2-4 sætninger om hvad kapitlet siger. Brug
          kapitlerne fra noterne; slå meget små afsnit sammen, så der højst er 20.
        Egne ord, ingen lange citater, kun det noterne siger. Svar ved at kalde værktøjet report_summary.
        """;

    private static readonly Tool PartTool = new()
    {
        Name = "report_part",
        Description = "Notes on these pages and the chapters that start in them",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["notes"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["chapters"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "object",
                      "properties": { "title": { "type": "string" }, "page": { "type": "integer" } },
                      "required": ["title", "page"] } }
                    """).RootElement,
            },
            Required = ["notes", "chapters"],
        },
    };

    private static readonly Tool SummaryTool = new()
    {
        Name = "report_summary",
        Description = "The summary of the whole book",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["summary"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["points"] = JsonDocument.Parse("""{ "type": "array", "items": { "type": "string" } }""").RootElement,
                ["chapters"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "object",
                      "properties": { "title": { "type": "string" }, "page": { "type": "integer" }, "text": { "type": "string" } },
                      "required": ["title", "page", "text"] } }
                    """).RootElement,
            },
            Required = ["summary", "points", "chapters"],
        },
    };

    public async Task<PartNote> ReadPartAsync(string title, int from, int to, IReadOnlyList<string> pages, CancellationToken ct = default)
    {
        var text = new StringBuilder();
        for (var i = 0; i < pages.Count; i++) text.Append($"[Side {from + i}]\n").Append(pages[i]).Append("\n\n");
        var body = SummaryPlan.Cut(text.ToString());
        if (body.Count(char.IsLetter) < 200)
            return new PartNote(from, to, "Næsten ingen tekst på disse sider (billeder eller tomme sider).", []);
        var input = await CallAsync(PartModel, 4000, PartSystem, PartTool, $"Bog: {title}\nSide {from}-{to}:\n\n{body}", ct);
        var r = input.Deserialize<PartReply>(Json) ?? new PartReply();
        return new PartNote(from, to, (r.Notes ?? "").Trim(),
            (r.Chapters ?? []).Where(c => c.Page >= from && c.Page <= to && !string.IsNullOrWhiteSpace(c.Title))
                .Select(c => new Chapter(c.Title!.Trim(), c.Page, "")).ToList());
    }

    public async Task<(string Summary, List<string> Points, List<Chapter> Chapters)> SummariseAsync(string title, int pages,
        IReadOnlyList<PartNote> notes, CancellationToken ct = default)
    {
        var ask = new StringBuilder($"Bog: {title} ({pages} sider)\n\n");
        foreach (var n in notes)
        {
            ask.Append($"## Side {n.From}-{n.To}\n{n.Notes}\n");
            foreach (var c in n.Chapters) ask.Append($"Kapitel starter s. {c.Page}: {c.Title}\n");
            ask.Append('\n');
        }
        var input = await CallAsync(SummaryModel, 8000, SummarySystem, SummaryTool, ask.ToString(), ct);
        var r = input.Deserialize<SummaryReply>(Json) ?? new SummaryReply();
        return ((r.Summary ?? "").Trim(), r.Points ?? [],
            (r.Chapters ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Title))
                .Select(c => new Chapter(c.Title!.Trim(), c.Page, (c.Text ?? "").Trim())).ToList());
    }

    // One counted call with a forced tool answer; the day's cap is checked and counted before the call goes out.
    private async Task<JsonElement> CallAsync(string model, int maxTokens, string system, Tool tool, string ask, CancellationToken ct)
    {
        await CountAsync(ct);
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxTokens,
            System = system,
            Tools = [tool],
            ToolChoice = new ToolChoiceTool { Name = tool.Name },
            Messages = [new() { Role = Role.User, Content = ask }],
        }, ct);
        log.LogInformation("Læs: {Tool} by {Model}, {In}/{Out} tokens", tool.Name, response.Model, response.Usage.InputTokens, response.Usage.OutputTokens);
        var used = response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault()
                   ?? throw new InvalidOperationException("AI'en svarede ikke med et resultat.");
        return JsonSerializer.SerializeToElement(used.Input);
    }

    private async Task CountAsync(CancellationToken ct)
    {
        await CapGate.WaitAsync(ct);
        try
        {
            await using var db = await dbf.CreateDbContextAsync(ct);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var day = await db.AiDays.FindAsync([today], ct);
            if (day is null) { day = new AiDay { Day = today }; db.AiDays.Add(day); }
            if (day.Calls >= MaxCallsPerDay)
                throw new InvalidOperationException($"Dagens grænse på {MaxCallsPerDay} AI-kald er nået - fortsætter i morgen.");
            day.Calls++;
            await db.SaveChangesAsync(ct);
        }
        finally { CapGate.Release(); }
    }

    private sealed class PartReply
    {
        public string? Notes { get; set; }
        public List<ChapterReply>? Chapters { get; set; }
    }

    private sealed class SummaryReply
    {
        public string? Summary { get; set; }
        public List<string>? Points { get; set; }
        public List<ChapterReply>? Chapters { get; set; }
    }

    private sealed class ChapterReply
    {
        public string? Title { get; set; }
        public int Page { get; set; }
        public string? Text { get; set; }
    }
}
