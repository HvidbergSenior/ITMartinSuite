using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinMitEl.Server.Services;

public sealed record LookupProgram(string Name, double Kwh);

public sealed record LookupResult(
    string Name, string Icon, string Model, double DurationHours,
    List<LookupProgram> Programs, string Note, List<string> Sources);

/// <summary>"Asko W6884" -> what kind of machine it is, how long a run takes and
/// the kWh per program, looked up on the web by Claude. One call per button
/// press, with a hard daily cap - the lookup is a convenience, not a loop.</summary>
public sealed class ApplianceLookup(IConfiguration config, ILogger<ApplianceLookup> logger)
{
    private const int MaxPerDay = 40;
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private const string System =
        "Du slår husholdningsapparater op for en dansk elpris-app. Brugeren skriver et modelnummer " +
        "(fx 'Asko W6884'). Søg på nettet efter apparatets energimærke, datablad eller manual og find:\n" +
        "- hvad det er (vaskemaskine, opvaskemaskine, tørretumbler, ovn ...)\n" +
        "- hvor lang tid én tur typisk tager i timer (brug standardprogrammet; en vaskemaskine er typisk 2)\n" +
        "- kWh pr. tur for de almindelige programmer. Vaskemaskine: 30, 40, 60 og 90 °C. " +
        "Opvaskemaskine: Eco, Normal, Intensiv. Andre apparater: ét program 'Standard'.\n" +
        "Brug tal fra producentens datablad eller energimærke når de findes. Findes der kun et årsforbrug, " +
        "så regn det om og skriv det i note. Findes intet om modellen, så brug typiske tal for den slags " +
        "apparat og skriv det tydeligt i note.\n" +
        "Programmet brugeren kører oftest skal stå først (vaskemaskine: 40 °C, opvaskemaskine: Eco).\n" +
        "Svar til sidst med KUN ét JSON-objekt og intet andet:\n" +
        "{\"name\": \"Vaskemaskine\", \"icon\": \"🧺\", \"model\": \"Asko W6884\", \"durationHours\": 2, " +
        "\"programs\": [{\"name\": \"40 °C\", \"kwh\": 0.7}], \"note\": \"kort dansk forklaring på hvor tallene kommer fra\"}";

    public async Task<LookupResult> LookupAsync(string modelText, CancellationToken ct = default)
    {
        Take();
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Mangler Claude-nøgle på serveren.");
        var client = new AnthropicClient { ApiKey = apiKey };

        var res = await client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-opus-5",
            MaxTokens = 4000,
            System = System,
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            Tools = [new ToolUnion(new WebSearchTool20260209 { MaxUses = 4 })],
            Messages = [new() { Role = Role.User, Content = $"Modelnummer: {modelText.Trim()}" }],
        }, ct);

        var sources = new List<string>();
        foreach (var block in res.Content)
            if (block.TryPickWebSearchToolResult(out var wr))
                sources.AddRange(Urls(wr));

        if (res.StopReason == "refusal") throw new InvalidOperationException("AI'en ville ikke svare på det modelnummer.");

        var text = string.Concat(res.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("Appliance lookup for {Model} gave no JSON: {Text}", modelText, text);
            throw new InvalidOperationException("Kunne ikke finde tal for den model. Prøv at skrive mærke + model.");
        }

        var raw = JsonSerializer.Deserialize<Raw>(text[start..(end + 1)], Json)
                  ?? throw new InvalidOperationException("Tomt svar.");
        var programs = (raw.Programs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Kwh is > 0 and < 100)
            .Select(p => new LookupProgram(p.Name!.Trim(), Math.Round(p.Kwh, 2)))
            .ToList();
        if (programs.Count == 0) throw new InvalidOperationException("Fandt ingen kWh-tal for den model.");

        logger.LogInformation("Appliance lookup {Model}: {Name}, {Count} programs", modelText, raw.Name, programs.Count);
        return new LookupResult(
            string.IsNullOrWhiteSpace(raw.Name) ? "Maskine" : raw.Name.Trim(),
            string.IsNullOrWhiteSpace(raw.Icon) ? "🔌" : raw.Icon.Trim(),
            string.IsNullOrWhiteSpace(raw.Model) ? modelText.Trim() : raw.Model.Trim(),
            raw.DurationHours is > 0 and <= 12 ? raw.DurationHours : 2,
            programs,
            raw.Note?.Trim() ?? "",
            sources.Distinct().Take(3).ToList());
    }

    private void Take()
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= MaxPerDay) throw new InvalidOperationException("Dagens opslag er brugt op. Prøv igen i morgen.");
            _used++;
        }
    }

    private static IEnumerable<string> Urls(WebSearchToolResultBlock wr) =>
        wr.Content.TryPickWebSearchResultBlocks(out var list) ? list.Select(r => r.Url) : [];

    private sealed class Raw
    {
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Model { get; set; }
        public double DurationHours { get; set; }
        public List<RawProgram>? Programs { get; set; }
        public string? Note { get; set; }
    }

    private sealed class RawProgram
    {
        public string? Name { get; set; }
        public double Kwh { get; set; }
    }
}
