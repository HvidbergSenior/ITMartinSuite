using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinPolstrer.Core.Data.Entities;
using ITMartinPolstrer.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartinPolstrer.Server.Services;

// One batched Claude call over a piece's photos + the polstrer's own notes.
// Returns SUGGESTIONS only - the page fills empty fields, her text always wins.
public sealed class AiSuggestionService(IConfiguration config, MediaStore media, ILogger<AiSuggestionService> logger)
{
    public enum Level { Hurtig, Grundig }

    public sealed record Suggestion(
        string NavnType, string Type, string PeriodeStil, string Materialer, string Konstruktion,
        string Stand, string[] Teknikker, string[] SaadanGoerDu, string Usikkerhed)
    {
        public string Model { get; init; } = "";
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }

        // Rough price in øre so the user sees what a call cost (Haiku 1/5 $, Opus 5/25 $ per MTok, ~7 kr/$).
        public double Oere => Model.Contains("haiku")
            ? (InputTokens * 1.0 + OutputTokens * 5.0) / 1_000_000 * 700
            : (InputTokens * 5.0 + OutputTokens * 25.0) / 1_000_000 * 700;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int MaxPhotos = 6;
    private const int MaxCallsPerDay = 40;
    private static int _callsToday;
    private static DateOnly _callsDay = DateOnly.FromDateTime(DateTime.UtcNow);

    public bool Enabled { get; } = !string.IsNullOrWhiteSpace(config["Claude:ApiKey"]);

    private static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly Tool SuggestTool = new()
    {
        Name = "foreslaa_journal",
        Description = "Forslag til udfyldning af polstrerens journal for møblet",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["navnType"] = Schema("""{"type":"string","description":"Kort navn på møblet, fx 'Wegner-lænestol, høj ryg'"}"""),
                ["type"] = Schema("""{"type":"string","enum":["Stol","Lænestol","Sofa","Skammel / puf","Bænk","Sengegavl","Bil / båd","Andet"]}"""),
                ["periodeStil"] = Schema("""{"type":"string","description":"Periode og stil, fx '1960erne, dansk modernisme'"}"""),
                ["materialer"] = Schema("""{"type":"string","description":"Materialer der kan ses: træsort, stof, skum, fjedre, gjord"}"""),
                ["konstruktion"] = Schema("""{"type":"string","description":"Hvordan møblet er bygget og polstret, og hvad det betyder for arbejdet"}"""),
                ["stand"] = Schema("""{"type":"string","description":"Tilstand set på billederne"}"""),
                ["teknikker"] = Schema("""{"type":"array","items":{"type":"string"},"description":"Korte polstrer-teknik-tags der sandsynligvis bliver brugt, fx 'fjedre', 'nakkerulle', 'kantsyning'"}"""),
                ["saadanGoerDu"] = Schema("""{"type":"array","items":{"type":"string"},"description":"Foreslået rækkefølge af arbejdstrin med konkrete faglige råd - byg videre på polstrerens egne noter, gentag dem ikke bare"}"""),
                ["usikkerhed"] = Schema("""{"type":"string","description":"Hvad du ikke kan se eller er usikker på"}"""),
            },
            Required = ["navnType", "type", "periodeStil", "materialer", "konstruktion", "stand", "teknikker", "saadanGoerDu", "usikkerhed"],
        },
    };

    public async Task<Suggestion> SuggestAsync(Piece piece, Level level, CancellationToken ct = default)
    {
        if (!Enabled) throw new InvalidOperationException("Claude:ApiKey mangler");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _callsDay) { _callsDay = today; _callsToday = 0; }
        if (_callsToday >= MaxCallsPerDay) throw new InvalidOperationException($"Dagens grænse på {MaxCallsPerDay} AI-opslag er nået");
        _callsToday++;

        var model = level == Level.Grundig ? "claude-opus-5" : "claude-haiku-4-5";
        var content = new List<ContentBlockParam>();
        var photos = piece.Steps.OrderBy(s => s.At).SelectMany(s => s.Media).Where(m => !m.IsVideo).Take(MaxPhotos).ToList();
        foreach (var m in photos)
        {
            var path = Path.Combine(media.Root, m.RelativePath);
            if (!File.Exists(path)) continue;
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = await ToJpegBase64Async(path, ct), MediaType = "image/jpeg" } });
        }

        var notes = string.Join("\n", piece.Steps.OrderBy(s => s.At).Where(s => !string.IsNullOrWhiteSpace(s.Note)).Select(s => "- " + s.Note.Trim()));
        content.Add(new TextBlockParam
        {
            Text = $"""
                Møbel: {piece.Title}
                Type: {piece.Category}
                Stof/materiale: {piece.Material}
                Kunde: {piece.Customer}
                Hvad blev der sagt ({piece.InstructedBy}): {piece.Instructions}
                Teknikker: {piece.Techniques}
                Trin indtil nu:
                {notes}

                Se billederne og noterne, og kald foreslaa_journal med dine forslag.
                """
        });

        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 1500,
            System = """
                Du er en erfaren dansk møbelpolstrer der hjælper en kollega med at dokumentere et møbel.
                Svar på dansk, kort og fagligt konkret. Gæt ikke på ting du ikke kan se - skriv dem under usikkerhed.
                Polstrerens egne noter er autoritative; dine forslag skal supplere dem med faglig viden om
                møbeltypen, konstruktionen og den typiske arbejdsgang - ikke gentage dem.
                """,
            Tools = [SuggestTool],
            ToolChoice = new ToolChoiceTool { Name = "foreslaa_journal" },
            Messages = [new() { Role = Role.User, Content = content }],
        };

        var client = new AnthropicClient { ApiKey = config["Claude:ApiKey"]! };
        var response = await client.Messages.Create(request, ct);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        if (toolUse is null) throw new InvalidOperationException("Intet forslag i svaret");

        var json = JsonSerializer.Serialize(toolUse.Input);
        logger.LogInformation("AI-forslag ({Model}, {Photos} fotos, {In}/{Out} tokens): {Json}", model, photos.Count, response.Usage.InputTokens, response.Usage.OutputTokens, json);
        var s = JsonSerializer.Deserialize<Suggestion>(json, JsonOptions) ?? throw new InvalidOperationException("Kunne ikke læse forslaget");
        return s with { Model = model, InputTokens = (int)response.Usage.InputTokens, OutputTokens = (int)response.Usage.OutputTokens };
    }

    private static async Task<string> ToJpegBase64Async(string path, CancellationToken ct)
    {
        using var image = await Image.LoadAsync(path, ct);
        const int MaxWidth = 1600;
        if (image.Width > MaxWidth)
            image.Mutate(x => x.Resize(MaxWidth, (int)(image.Height * ((double)MaxWidth / image.Width))));
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 }, ct);
        return Convert.ToBase64String(ms.ToArray());
    }
}
