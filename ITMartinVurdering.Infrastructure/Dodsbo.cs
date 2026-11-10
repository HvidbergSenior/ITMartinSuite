using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using ITMartinVurdering.Application;
using ITMartinVurdering.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinVurdering.Infrastructure;

// One folder per Sag on the data volume: DataDir/sager/<id>/sag.json + <tingId>.jpg (the phone shrinks the photo before upload).
public sealed class JsonSagStore : ISagStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _root;
    private readonly object _lock = new();

    public JsonSagStore(IConfiguration cfg)
    {
        _root = Path.Combine(cfg["DataDir"] ?? "/data", "sager");
        Directory.CreateDirectory(_root);
    }

    // Ids are made here (12 hex) - anything else never names a folder or a file.
    private static bool SafeId(string id) => id.Length is > 0 and <= 32 && id.All(char.IsAsciiLetterOrDigit);
    private string Dir(string sagId) => SafeId(sagId) ? Path.Combine(_root, sagId) : throw new ArgumentException("Ukendt sag.");

    public List<Sag> All()
    {
        lock (_lock)
            return Directory.EnumerateDirectories(_root).Select(d => Read(Path.Combine(d, "sag.json"))).OfType<Sag>().ToList();
    }

    public Sag? Get(string id)
    {
        if (!SafeId(id)) return null;
        lock (_lock) return Read(Path.Combine(_root, id, "sag.json"));
    }

    private static Sag? Read(string path) => File.Exists(path) ? JsonSerializer.Deserialize<Sag>(File.ReadAllText(path), Json) : null;

    public void Save(Sag sag)
    {
        lock (_lock)
        {
            var dir = Dir(sag.Id);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "sag.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(sag, Json));
            File.Move(path + ".tmp", path, overwrite: true);
        }
    }

    public void SavePhoto(string sagId, string tingId, byte[] jpeg)
    {
        if (!SafeId(tingId)) throw new ArgumentException("Ukendt ting.");
        var dir = Dir(sagId);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, tingId + ".jpg"), jpeg);
    }

    public byte[]? Photo(string sagId, string tingId)
    {
        if (!SafeId(sagId) || !SafeId(tingId)) return null;
        var path = Path.Combine(_root, sagId, tingId + ".jpg");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void DeletePhoto(string sagId, string tingId)
    {
        if (!SafeId(sagId) || !SafeId(tingId)) return;
        var path = Path.Combine(_root, sagId, tingId + ".jpg");
        if (File.Exists(path)) File.Delete(path);
    }
}

// Values up to 8 photos in ONE call (CLAUDE.md cost rules) under a hard daily cap counted in DataDir/ai-calls.json.
// Model: Vurdering:AiModel (default claude-opus-5-5 - a wrong low price makes a valuable thing go too cheap). A policy
// decline is re-served by the fallback model in the same call (server-side fallbacks).
public sealed class ClaudeThingValuer(IConfiguration cfg, ILogger<ClaudeThingValuer> log) : IThingValuer
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly object CapLock = new();

    private string Model => cfg["Vurdering:AiModel"] is { Length: > 0 } m ? m : "claude-opus-5-5";
    private int MaxCallsPerDay => int.TryParse(cfg["Vurdering:MaxAiCallsPerDay"], out var n) ? n : 20;
    private string CapFile => Path.Combine(cfg["DataDir"] ?? "/data", "ai-calls.json");
    public bool Enabled => !string.IsNullOrWhiteSpace(cfg["Claude:ApiKey"]);

    private sealed record CapDay(string Day, int Calls);
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    private CapDay ReadCap()
    {
        try { return File.Exists(CapFile) ? JsonSerializer.Deserialize<CapDay>(File.ReadAllText(CapFile)) is { } c && c.Day == Today ? c : new(Today, 0) : new(Today, 0); }
        catch (JsonException) { return new(Today, 0); }
    }

    public int CallsLeftToday() { lock (CapLock) return Math.Max(0, MaxCallsPerDay - ReadCap().Calls); }

    /// <summary>Counted BEFORE the call, so a crash or a retry can never go over the cap.</summary>
    private void Reserve()
    {
        lock (CapLock)
        {
            var c = ReadCap();
            if (c.Calls >= MaxCallsPerDay) throw new InvalidOperationException("Dagens grænse for AI-vurderinger er nået.");
            Directory.CreateDirectory(Path.GetDirectoryName(CapFile)!);
            File.WriteAllText(CapFile, JsonSerializer.Serialize(c with { Calls = c.Calls + 1 }));
        }
    }

    private const string System = """
        Du vurderer ting fra dødsboer, lofter og kældre for en dansk sælger, der vil vide, hvad tingene er, hvad de er værd
        brugt i Danmark i dag, og hvad man skal gøre med dem. Hvert billede viser én ting (eller et sæt, der hører sammen).
        For hvert nummereret billede:
        - what: hvad det er, kort (fx "Rubinglas-vase, böhmisk, slebet"). Kendt mærke, kunstner, fabrik eller periode, hvis du kan se det.
        - details: det, der afgør værdien - stempler, signatur, mærker, stand, alder - og hvad sælgeren skal kigge efter (fx "vend den og se efter stempel i bunden").
        - low/high: realistisk salgspris i kr. brugt i Danmark (DBA, auktion, loppemarked) - ikke drømmepriser og ikke nypris.
          Er du ikke sikker, så lad low/high være null og vælg "spørg en ekspert".
        - verdict: "sælg" (værd at sælge alene, typisk 200 kr. eller mere), "sælg samlet" (lav værdi alene, sælg i kasse/parti),
          "giv væk" (næsten ingen salgsværdi, men brugbar), "smid ud" (ødelagt eller uden værdi), "spørg en ekspert"
          (kan være værdifuld - kunst med signatur, sølv, ældre porcelæn, smykker - eller du er ikke sikker).
        - where: det bedste sted at sælge: DBA, Lauritz.com, Bruun Rasmussen (kun dyre, kendte ting), Loppemarked, Bogshoppen (bøger, blade, plader, film, spil), Genbrugsbutik.
        - reason: én kort sætning på dansk om, hvorfor.
        - searchWords: 2-5 søgeord til DBA/Lauritz (fx "böhmisk rubinglas vase").
        - story: hvad tingen er og historien bag den, 3-5 sætninger på almindeligt dansk, så sælgeren kan fortælle om den:
          hvor og hvornår den slags blev lavet, af hvem (fabrik, kunstner, egn), hvordan den blev lavet og brugt, og hvorfor
          folk samler på den. Skriv kun det, du er sikker på - er du i tvivl, så skriv det generelle om typen og sig, at det
          afhænger af stempel eller signatur.
        Gæt aldrig et navn, en signatur eller et stempel, du ikke kan læse. Hellere "spørg en ekspert" end en forkert lav pris.
        Svar ved at kalde værktøjet report_things med én post pr. billede.
        """;

    private static readonly BetaTool ReportTool = new()
    {
        Name = "report_things",
        Description = "Report what each numbered photo shows, its value in kr and what to do with it",
        Strict = true,
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["items"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "object",
                      "properties": {
                        "index":       { "type": "integer" },
                        "what":        { "type": "string" },
                        "details":     { "type": "string" },
                        "low":         { "type": ["integer", "null"] },
                        "high":        { "type": ["integer", "null"] },
                        "verdict":     { "type": "string", "enum": ["sælg", "sælg samlet", "giv væk", "smid ud", "spørg en ekspert"] },
                        "where":       { "type": "string", "enum": ["DBA", "Lauritz.com", "Bruun Rasmussen", "Loppemarked", "Bogshoppen", "Genbrugsbutik"] },
                        "reason":      { "type": "string" },
                        "searchWords": { "type": "string" },
                        "story":       { "type": "string" } },
                      "required": ["index", "what", "details", "low", "high", "verdict", "where", "reason", "searchWords", "story"],
                      "additionalProperties": false } }
                    """).RootElement,
            },
            Required = ["items"],
        },
    };

    private sealed class Report { public List<Item> Items { get; set; } = []; }
    private sealed class Item
    {
        public int Index { get; set; }
        public string What { get; set; } = "";
        public string Details { get; set; } = "";
        public int? Low { get; set; }
        public int? High { get; set; }
        public string Verdict { get; set; } = "";
        public string Where { get; set; } = "";
        public string Reason { get; set; } = "";
        public string SearchWords { get; set; } = "";
        public string Story { get; set; } = "";
    }

    public async Task<List<ThingValue>> ValueAsync(IReadOnlyList<ThingPhoto> photos, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");
        if (photos.Count == 0) return [];
        if (photos.Count > DodsboRules.PerCall) throw new ArgumentException($"Højst {DodsboRules.PerCall} billeder ad gangen.");
        Reserve();

        var content = new List<BetaContentBlockParam>();
        foreach (var p in photos)
        {
            content.Add(new BetaTextBlockParam { Text = $"Billede {p.Index}:" + (p.Hint.Length > 0 ? $" (sælgeren skriver: {p.Hint})" : "") });
            content.Add(new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(p.Jpeg), MediaType = "image/jpeg" } });
        }
        content.Add(new BetaTextBlockParam { Text = $"Vurdér alle {photos.Count} billeder (nummer {photos[0].Index} til {photos[^1].Index}) og kald report_things." });

        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 16000,
            System = System,
            Tools = [ReportTool],
            ToolChoice = new BetaToolChoiceAuto(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = [new BetaFallbackParam { Model = "claude-opus-4-8" }],
            Messages = [new() { Role = Role.User, Content = content }],
        }, ct);
        log.LogInformation("Dødsbo: {Count} photos valued by {Model}, stop {Stop}, {In}/{Out} tokens", photos.Count, response.Model,
            response.StopReason, response.Usage.InputTokens, response.Usage.OutputTokens);

        var tool = response.Content.Select(b => b.Value).OfType<BetaToolUseBlock>().FirstOrDefault();
        if (tool is null) throw new InvalidOperationException("AI'en gav ikke et svar på billederne. Prøv igen om lidt.");
        var report = JsonSerializer.Deserialize<Report>(JsonSerializer.Serialize(tool.Input), Json) ?? new Report();
        return report.Items.Select(i => new ThingValue(i.Index, i.What, i.Details, i.Low, i.High, i.Verdict, i.Where, i.Reason, i.SearchWords, i.Story)).ToList();
    }
}
