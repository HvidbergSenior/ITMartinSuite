using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.External;

// Values a pile of books: the price sites' numbers in, price + demand + verdict out - up to 40 books in ONE call
// (CLAUDE.md cost rules), under a hard daily cap (Lager:MaxAiCallsPerDay) counted in the database. Reading covers,
// kolofons and writing collector texts moved to Vareinfo at the split (2026-10-12).
public sealed class ClaudeBookValuer(IConfiguration cfg, IDbContextFactory<LagerDb> dbf, ILogger<ClaudeBookValuer> log) : IBookValuer
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim CapGate = new(1, 1);

    private string Model => cfg["Lager:AiModel"] ?? "claude-haiku-4-5";
    public int MaxCallsPerDay => int.TryParse(cfg["Lager:MaxAiCallsPerDay"], out var n) ? n : 800;
    // Valuing gets the most accurate model (a wrong low price sells a valuable book too cheap); the cheap model is the
    // fallback. Lager__InterestModel switches it (e.g. claude-sonnet-5-5).
    private string InterestModel => cfg["Lager:InterestModel"] ?? "claude-opus-5-5";
    public bool Enabled => !string.IsNullOrWhiteSpace(cfg["Claude:ApiKey"]);

    public async Task<int> CallsTodayAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Now);
        return (await db.AiDays.FindAsync(today))?.Calls ?? 0;
    }

    // One call per photo: the whole pile plus up to four close-ups of it in the same message (never a call per close-up).
    // A refusal, an error or no tool call -> the cheap model with a forced call, so a pile is never left without an answer.
    private async Task<(ToolUseBlock? Tool, Message? Response)> AccurateToolCallAsync(string system, Tool tool, string ask, CancellationToken ct)
    {
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        Message? response = null;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = InterestModel,
                MaxTokens = 16000,
                System = system,
                Tools = [tool],
                ToolChoice = new ToolChoiceAuto(),
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = [new() { Role = Role.User, Content = ask }],
            }, ct);
            await CountTokensAsync(response, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Accurate model {Model} failed - falling back to {Cheap}", InterestModel, Model);
        }

        var used = response?.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault();
        if (used is not null) return (used, response);
        if (response is not null) log.LogWarning("Accurate model gave no tool call (stop: {Stop}) - falling back", response.StopReason);
        response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 8000,
            System = system,
            Tools = [tool],
            ToolChoice = new ToolChoiceTool { Name = tool.Name },
            Messages = [new() { Role = Role.User, Content = ask }],
        }, ct);
        await CountTokensAsync(response, ct);
        return (response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault(), response);
    }

    private const string ValueSystem = """
        Du vurderer brugte bøger for en dansk butik, der skal sælge mange bøger hurtigt og vil vide, hvilke der er
        værd at sælge enkeltvis. For hver bog: en realistisk salgspris i kr. for et brugt eksemplar i den angivne stand
        (lav-høj), efterspørgslen (Høj, Middel, Lav) og en dom:
        - "Sælg": værd at sætte til salg enkeltvis (typisk 75 kr. eller mere, eller eftertragtet).
        - "Kasse": sælges bedst i en kasse/bunke med andre (typisk 20-75 kr.).
        - "Genbrug": næsten ingen værdi - masseudgivelse, bogklub-udgave, forældet fagbog.
        - "Tjek selv": du er IKKE sikker på værdien. Så giv INGEN pris (low/high = null). Brug den hellere end at gætte -
          et forkert lavt bud får butikken til at sælge en værdifuld bog for billigt.
        Hvert nummer kan have markedstal (udbudspriser, ikke solgte) fra DBA (Danmark), AbeBooks (internationalt),
        Bokbörsen (svenske og nordiske antikvariater, også danske bøger), Nemos Bibliotek og Faraos (danske antikvariater for tegneserier og samlerbøger; Faraos
        har én pris pr. stand), omregnet til kr. "Ny i butikken" er nyprisen - et loft for et brugt eksemplar, ikke bevis.
        BEVIS: en pris må KUN gives, når den bygger på markedstal for netop denne bog. Skriv i "basis" hvilke kilder
        prisen bygger på. Ingen kilde med priser = "Tjek selv" uden pris - aldrig en standardpris som 20-40 kr.
        Markedstal, der står som "ikke bevis", må ikke bruges. "Urimelige internetpriser" er drømmepriser
        - brug dem ikke som pris.
        Hvor skal den sælges (where): DBA = danske bøger og alt til danske købere; AbeBooks = antikvariske, samler- og
        engelske/udenlandske bøger med en international køberskare; Bokbörsen = nordiske samler- og antikvariske bøger
        med købere i Sverige/Norden; eBay = samlerobjekter med budkrig (sjældne førsteudgaver, signerede); Bogshoppen =
        kassevarer og billige bøger, der sælges i butikken. Engelsksprogede og samlerbøger (fx Warhammer/Black Library,
        udgåede omnibusser, fantasy/sci-fi-førsteudgaver) sælges på det internationale marked - brug AbeBooks for dem.
        Mange til salg billigt = lav efterspørgsel. Få eller ingen til salg kan betyde sjælden ELLER uinteressant: uden
        markedstal og uden sikker viden = "Tjek selv".
        Grunden: én kort sætning på dansk med det, der afgør det. Skriv kun fakta, du er sikker på.
        Svar ved at kalde værktøjet report_values med én post pr. nummer.
        """;

    private static readonly Tool ValueTool = new()
    {
        Name = "report_values",
        Description = "Report price, demand and verdict for each numbered book",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["items"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "object",
                      "properties": {
                        "index":   { "type": "integer" },
                        "low":     { "type": ["integer", "null"], "description": "kr, null when verdict is Tjek selv" },
                        "high":    { "type": ["integer", "null"], "description": "kr, null when verdict is Tjek selv" },
                        "demand":  { "type": "string", "enum": ["Høj", "Middel", "Lav"] },
                        "verdict": { "type": "string", "enum": ["Sælg", "Kasse", "Genbrug", "Tjek selv"] },
                        "reason":  { "type": "string" },
                        "where":   { "type": "string", "enum": ["DBA", "AbeBooks", "Bokbörsen", "eBay", "Bogshoppen", "Nemos Bibliotek", "Faraos"] },
                        "basis":   { "type": "array", "items": { "type": "string", "enum": ["DBA", "AbeBooks", "Bokbörsen", "Nemos Bibliotek", "Faraos"] },
                                     "description": "The market sources whose prices for THIS book the price is based on. Empty = no proof." } },
                      "required": ["index", "demand", "verdict", "reason"], "additionalProperties": false } }
                    """).RootElement,
            },
            Required = ["items"],
        },
    };

    // Price + demand + verdict for up to 40 books in ONE text-only call (never one call per book).
    public async Task<Dictionary<int, BookValue>> ValueBooksAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        lines = lines.Take(40).ToList();
        if (lines.Count == 0) return [];
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");
        await ReserveCallAsync(ct);
        var ask = "Vurdér disse bøger:\n" + string.Join("\n", lines.Select((l, i) => $"{i}: {l}"));
        var (tool, response) = await AccurateToolCallAsync(ValueSystem, ValueTool, ask, ct);
        if (tool is null) return [];
        var parsed = JsonSerializer.Deserialize<ValueReport>(JsonSerializer.Serialize(tool.Input), Json) ?? new ValueReport();
        log.LogInformation("Values for {Count}/{Asked} books, {In}/{Out} tokens", parsed.Items.Count, lines.Count,
            response!.Usage.InputTokens, response.Usage.OutputTokens);
        return parsed.Items.Where(x => x.Index >= 0 && x.Index < lines.Count)
            .GroupBy(x => x.Index).ToDictionary(g => g.Key, g => g.First().ToValue());
    }


    private async Task CountTokensAsync(Message response, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var day = await db.AiDays.FindAsync([DateOnly.FromDateTime(DateTime.Now)], ct);
        if (day is null) return;
        day.InputTokens += response.Usage.InputTokens;
        day.OutputTokens += response.Usage.OutputTokens;
        await db.SaveChangesAsync(ct);
    }

    // Reserve the call BEFORE making it - the cap holds even if the call then fails.
    private async Task ReserveCallAsync(CancellationToken ct)
    {
        await CapGate.WaitAsync(ct);
        try
        {
            await using var db = await dbf.CreateDbContextAsync(ct);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var day = await db.AiDays.FindAsync([today], ct);
            if (day is null) db.AiDays.Add(day = new AiDay { Day = today });
            if (day.Calls >= MaxCallsPerDay)
                throw new InvalidOperationException($"Dagens grænse på {MaxCallsPerDay} AI-kald er nået. Fortsæt i morgen, eller hæv Lager__MaxAiCallsPerDay.");
            day.Calls++;
            await db.SaveChangesAsync(ct);
        }
        finally { CapGate.Release(); }
    }

    internal sealed class ValueReport { public List<ValueRow> Items { get; set; } = []; }
    internal sealed class ValueRow
    {
        public BookValue ToValue() => BookValue.Create(Low, High, Demand, Verdict, Reason, Where, Basis);
        public int Index { get; set; }
        public int? Low { get; set; }
        public int? High { get; set; }
        public string? Demand { get; set; }
        public string? Verdict { get; set; }
        public string? Reason { get; set; }
        public string? Where { get; set; }
        public string[]? Basis { get; set; }
    }
}
