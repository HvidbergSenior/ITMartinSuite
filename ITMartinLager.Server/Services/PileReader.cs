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
    string Platform, string Condition, int Quantity, double Confidence, string Note, string Interest = "");

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
    public int MaxCallsPerDay => int.TryParse(cfg["Lager:MaxAiCallsPerDay"], out var n) ? n : 400;
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
                          "note":       { "type": "string", "description": "Short note if something is unreadable or damaged" },
                          "interest":   { "type": "string", "description": "1-2 Danish sentences for a collector: what is interesting about exactly this item" }
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
        - interest: 1-2 sætninger på dansk til en samler om det interessante ved netop denne vare - fx tegner/forfatter,
          kendte historier eller indhold, hvad der var særligt det år, en kendt optræden, første/sidste nummer. Det er
          butikkens fokus, også når varen er billig. Skriv kun hvad du med rimelighed ved eller kan se på forsiden;
          ved du ikke noget særligt, så beskriv kort hvad det er og hvorfor folk samler på den slags. Opfind aldrig fakta.
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

        // Reserve the call BEFORE making it - the cap holds even if the call then fails.
        await CapGate.WaitAsync(ct);
        try
        {
            await using var db = await dbf.CreateDbContextAsync(ct);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var day = await db.AiDays.FindAsync([today], ct);
            if (day is null) db.AiDays.Add(day = new AiDay { Day = today });
            if (day.Calls >= MaxCallsPerDay)
                throw new InvalidOperationException($"Dagens grænse på {MaxCallsPerDay} AI-billeder er nået. Fortsæt i morgen, eller hæv Lager__MaxAiCallsPerDay.");
            day.Calls++;
            await db.SaveChangesAsync(ct);
        }
        finally { CapGate.Release(); }

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

        await using (var db = await dbf.CreateDbContextAsync(ct))
        {
            var day = await db.AiDays.FindAsync([DateOnly.FromDateTime(DateTime.Now)], ct);
            if (day is not null)
            {
                day.InputTokens += response.Usage.InputTokens;
                day.OutputTokens += response.Usage.OutputTokens;
                await db.SaveChangesAsync(ct);
            }
        }

        var tool = response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault()
            ?? throw new InvalidOperationException("AI'en svarede ikke med en liste. Prøv et nyt billede.");
        var parsed = JsonSerializer.Deserialize<Report>(JsonSerializer.Serialize(tool.Input), Json) ?? new Report();
        log.LogInformation("Pile read: {Count} items, {In}/{Out} tokens", parsed.Items.Count, response.Usage.InputTokens, response.Usage.OutputTokens);
        return parsed.Items.Select(Clean).Where(f => f.Title.Length > 0).ToList();
    }

    internal static Found Clean(RawItem r) => new(
        Kinds.Normalise(r.Kind), (r.Title ?? "").Trim(), (r.Artist ?? "").Trim(), (r.Series ?? "").Trim(),
        (r.Number ?? "").Trim(), r.Year is > 1800 and < 2100 ? r.Year : null, (r.Platform ?? "").Trim(),
        Conditions.Normalise(r.Condition), Math.Clamp(r.Quantity ?? 1, 1, 999), Math.Clamp(r.Confidence ?? 0.5, 0, 1),
        (r.Note ?? "").Trim(), (r.Interest ?? "").Trim());

    internal sealed class Report { public List<RawItem> Items { get; set; } = []; }

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
