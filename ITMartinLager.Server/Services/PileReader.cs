using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinLager.Server.Data;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinLager.Server.Services;

public sealed record Found(string Kind, string Title, string Artist, string Series, string Number, int? Year,
    string Platform, string Condition, int Quantity, double Confidence, string Note, string Interest = "", string Barcode = "");

// One photo of a PILE (10-20 covers side by side) -> one Claude call that reads every item. Never one call per item:
// 100,000 items is ~7,000 photos this way (CLAUDE.md cost rules). Cheap model by default (Lager:AiModel), and a hard
// daily cap (Lager:MaxAiCallsPerDay) counted in the database, so a restart or a loop cannot run up the bill.
public sealed class PileReader(IConfiguration cfg, IDbContextFactory<LagerDb> dbf, ILogger<PileReader> log)
{
    // Claude scales anything larger down to about this anyway; sending more only costs upload time.
    public const int MaxEdge = 1568;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim CapGate = new(1, 1);

    private string Model => cfg["Lager:AiModel"] ?? "claude-haiku-4-5";
    public int MaxCallsPerDay => int.TryParse(cfg["Lager:MaxAiCallsPerDay"], out var n) ? n : 800;
    // The collector text is the shop's focus, so it gets the most accurate model (user 2026-10-05: Haiku invented
    // a track list). Reading covers stays on the cheap model. Lager__InterestModel switches it (e.g. claude-sonnet-5-5).
    private string InterestModel => cfg["Lager:InterestModel"] ?? "claude-opus-5-5";
    public bool Enabled => !string.IsNullOrWhiteSpace(cfg["Claude:ApiKey"]);

    private static readonly Tool ReportTool = new()
    {
        Name = "report_items",
        Description = "Report every item visible in the photo",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["items"] = JsonDocument.Parse($$"""
                    {
                      "type": "array",
                      "description": "One entry per distinct item. Identical copies = one entry with quantity.",
                      "items": {
                        "type": "object",
                        "properties": {
                          "kind":       { "type": "string", "enum": {{JsonSerializer.Serialize(Kinds.All)}} },
                          "title":      { "type": "string", "description": "Title as printed (album title for a CD)" },
                          "artist":     { "type": "string", "description": "CD artist or book author, empty if none" },
                          "series":     { "type": "string", "description": "Series/magazine name, e.g. 'Anders And & Co.', 'Fantomet', 'Wendy'" },
                          "number":     { "type": "string", "description": "Issue/volume number as printed, e.g. '12', '23/1975'" },
                          "year":       { "type": ["integer", "null"] },
                          "platform":   { "type": "string", "description": "Console games only: 'PlayStation 2', 'Nintendo 64' ..." },
                          "condition":  { "type": "string", "enum": {{JsonSerializer.Serialize(Conditions.All)}} },
                          "quantity":   { "type": "integer", "minimum": 1 },
                          "confidence": { "type": "number", "description": "0-1: how sure the title/number reading is" },
                          "note":       { "type": "string", "description": "Short note if something is unreadable or damaged" }
                        },
                        "required": ["kind", "title", "condition", "quantity", "confidence"]
                      }
                    }
                    """).RootElement,
            },
            Required = ["items"],
        },
    };

    private const string System = """
        Du registrerer brugte varer til salg i en dansk butik: Anders And-blade, Anders And Jumbobøger, tegneserier,
        magasiner, bøger, CD'er, DVD'er, Blu-rays, konsolspil og Magic: The Gathering-kort.
        Billedet viser en bunke varer lagt ud side om side. Find HVER vare og kald report_items.
        - Læs titel, nummer og år præcist som de står på forsiden/ryggen. Gæt aldrig et nummer - lad det være tomt,
          og sæt confidence lavt, hvis det ikke kan læses.
        - Anders And & Co.: serie "Anders And & Co.", nummer og år fra forsiden. Jumbobog: kind "Jumbobog", nummer fra ryggen/forsiden.
        - CD: titel = albummets navn, artist = kunstneren. Konsolspil: platform (fx "PlayStation 2").
        - Magic-kort: titel = kortets navn, serie = udgivelsen hvis den kan ses.
        - Stand ud fra hvad der kan ses (rifter, fold, slid, mangler): Som ny, Meget god, God, Slidt, Defekt.
        - Ens eksemplarer = én post med quantity.
        """;

    // Phone photos are 3-12 MB; Claude only needs ~1568 px, so resize + JPEG before saving and sending.
    public static async Task<byte[]> ShrinkAsync(Stream input, CancellationToken ct)
    {
        using var img = await Image.LoadAsync(input, ct);
        img.Mutate(x => x.AutoOrient());
        if (img.Width > MaxEdge || img.Height > MaxEdge)
            img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxEdge, MaxEdge) }));
        using var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 82 }, ct);
        return ms.ToArray();
    }

    public async Task<int> CallsTodayAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);
        return (await db.AiDays.FindAsync(today))?.Calls ?? 0;
    }

    public async Task<List<Found>> ReadAsync(byte[] jpeg, string hint, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");

        await ReserveCallAsync(ct);
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var text = "Registrér alle varer på billedet." + (string.IsNullOrWhiteSpace(hint) ? "" : $" Det er mest: {hint}.");
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 12000,
            System = System,
            Tools = [ReportTool],
            ToolChoice = new ToolChoiceTool { Name = "report_items" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(jpeg), MediaType = "image/jpeg" } },
                        new TextBlockParam { Text = text },
                    },
                },
            ],
        }, ct);
        await CountTokensAsync(response, ct);

        var tool = response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault()
            ?? throw new InvalidOperationException("AI'en svarede ikke med en liste. Prøv et nyt billede.");
        var parsed = JsonSerializer.Deserialize<Report>(JsonSerializer.Serialize(tool.Input), Json) ?? new Report();
        log.LogInformation("Pile read: {Count} items, {In}/{Out} tokens", parsed.Items.Count, response.Usage.InputTokens, response.Usage.OutputTokens);
        return parsed.Items.Select(Clean).Where(f => f.Title.Length > 0).ToList();
    }

    private const string InterestSystem = """
        Du skriver korte tekster til en dansk butik, der sælger brugte bøger, Anders And-blade, Jumbobøger, tegneserier,
        magasiner, CD'er, DVD'er, Blu-rays, konsolspil og Magic-kort. Butikkens fokus er det interessante for en samler.
        For hver vare: 1-2 sætninger på korrekt dansk om hvad der er interessant ved netop den - tegner, forfatter,
        kunstner, hvad udgivelsen er kendt for, hvad der var særligt det år.
        Skriv KUN fakta, du er helt sikker på. Nævn ikke konkrete numre, historier, medvirkende eller årstal, medmindre du
        er sikker - hellere generelt og rigtigt end specifikt og forkert. Gentag ikke titlen ordret som det eneste.
        Svar ved at kalde værktøjet report_interest med én tekst pr. nummer.
        """;

    private static readonly Tool InterestTool = new()
    {
        Name = "report_interest",
        Description = "Report the collector text for each numbered item",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["items"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "object",
                      "properties": { "index": { "type": "integer" }, "interest": { "type": "string" } },
                      "required": ["index", "interest"], "additionalProperties": false } }
                    """).RootElement,
            },
            Required = ["items"],
        },
    };

    // "Det interessante" for up to 40 items in ONE text-only call (never one call per item).
    public async Task<Dictionary<int, string>> WriteInterestAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        lines = lines.Take(40).ToList();
        if (lines.Count == 0) return [];
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");
        await ReserveCallAsync(ct);
        var ask = "Skriv den interessante tekst for hver af disse varer:\n" + string.Join("\n", lines.Select((l, i) => $"{i}: {l}"));
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };

        // The accurate model first. Opus 5.5 / Sonnet 5.5 do not allow a forced tool call, so the prompt asks for it.
        // A refusal, an error or no tool call -> the cheap model with a forced call, so a pile is never left without text.
        Message? response = null;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = InterestModel,
                MaxTokens = 16000,
                System = InterestSystem,
                Tools = [InterestTool],
                ToolChoice = new ToolChoiceAuto(),
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = [new() { Role = Role.User, Content = ask }],
            }, ct);
            await CountTokensAsync(response, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Interest model {Model} failed - falling back to {Cheap}", InterestModel, Model);
        }

        var tool = response?.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault();
        if (tool is null)
        {
            if (response is not null) log.LogWarning("Interest model gave no tool call (stop: {Stop}) - falling back", response.StopReason);
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 8000,
                System = InterestSystem,
                Tools = [InterestTool],
                ToolChoice = new ToolChoiceTool { Name = "report_interest" },
                Messages = [new() { Role = Role.User, Content = ask }],
            }, ct);
            await CountTokensAsync(response, ct);
            tool = response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault();
            if (tool is null) return [];
        }
        var parsed = JsonSerializer.Deserialize<InterestReport>(JsonSerializer.Serialize(tool.Input), Json) ?? new InterestReport();
        log.LogInformation("Interest written for {Count}/{Asked} items, {In}/{Out} tokens", parsed.Items.Count, lines.Count,
            response!.Usage.InputTokens, response.Usage.OutputTokens);
        return parsed.Items.Where(x => x.Index >= 0 && x.Index < lines.Count && !string.IsNullOrWhiteSpace(x.Interest))
            .GroupBy(x => x.Index).ToDictionary(g => g.Key, g => g.First().Interest!.Trim());
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

    internal static Found Clean(RawItem r) => new(
        Kinds.Normalise(r.Kind), (r.Title ?? "").Trim(), (r.Artist ?? "").Trim(), (r.Series ?? "").Trim(),
        (r.Number ?? "").Trim(), r.Year is > 1800 and < 2100 ? r.Year : null, (r.Platform ?? "").Trim(),
        Conditions.Normalise(r.Condition), Math.Clamp(r.Quantity ?? 1, 1, 999), Math.Clamp(r.Confidence ?? 0.5, 0, 1),
        (r.Note ?? "").Trim(), (r.Interest ?? "").Trim());

    internal sealed class Report { public List<RawItem> Items { get; set; } = []; }
    internal sealed class InterestReport { public List<InterestRow> Items { get; set; } = []; }
    internal sealed class InterestRow { public int Index { get; set; } public string? Interest { get; set; } }

    internal sealed class RawItem
    {
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Series { get; set; }
        public string? Number { get; set; }
        public int? Year { get; set; }
        public string? Platform { get; set; }
        public string? Condition { get; set; }
        public int? Quantity { get; set; }
        public double? Confidence { get; set; }
        public string? Note { get; set; }
        public string? Interest { get; set; }
    }
}
