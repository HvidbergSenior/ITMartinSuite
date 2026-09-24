using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinMitEl.Server.Services;

public sealed record LookupProgram(string Name, double Kwh);

public sealed record LookupResult(
    string Name, string Icon, string Model, double DurationHours,
    List<LookupProgram> Programs, string Note, List<string> Sources, double CostUsd);

public sealed record DeviceLookupResult(
    string Name, string Icon, string Model, double Watts, double HoursPerDay, int? UsualFromHour,
    string Note, List<string> Sources, double CostUsd);

/// <summary>Model number in, energy numbers out: Claude's estimate from what it
/// knows about the model (no web search - an approximation is enough here and
/// costs a tenth).
/// Machines (washer, dishwasher ...) get run time + kWh per program; devices
/// (fridge, TV, router ...) get average watts + hours per day. One call per
/// button press, with a hard daily cap - the lookup is a convenience, not a loop.</summary>
public sealed class ApplianceLookup(IConfiguration config, ILogger<ApplianceLookup> logger)
{
    private const int MaxPerDay = 40;
    private const string ModelId = "claude-opus-5";
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private const string Common =
        "Du slår husholdningsapparater op for en dansk elpris-app. Brugeren skriver et modelnummer " +
        "(fx 'Asko W6884'). Søg på nettet efter apparatets energimærke, datablad eller manual. " +
        "Brug tal fra producenten eller energimærket når de findes. Findes intet om modellen, så brug " +
        "typiske tal for den slags apparat og skriv det tydeligt i note. Svar til sidst med KUN ét " +
        "JSON-objekt og intet andet.\n";

    private const string MachinePrompt = Common +
        "Apparatet kører en tur ad gangen. Find:\n" +
        "- hvad det er (vaskemaskine, opvaskemaskine, tørretumbler, ovn ...)\n" +
        "- hvor lang tid én tur typisk tager i timer (en vaskemaskine er typisk 2)\n" +
        "- kWh pr. tur for de almindelige programmer. Vaskemaskine: 30, 40, 60 og 90 °C. " +
        "Opvaskemaskine: Eco, Normal, Intensiv. Andre apparater: ét program 'Standard'.\n" +
        "Findes der kun et årsforbrug, så regn det om og skriv det i note. " +
        "Programmet der bruges oftest står først (vaskemaskine: 40 °C, opvaskemaskine: Eco).\n" +
        "JSON: {\"name\": \"Vaskemaskine\", \"icon\": \"🧺\", \"model\": \"Asko W6884\", \"durationHours\": 2, " +
        "\"programs\": [{\"name\": \"40 °C\", \"kwh\": 0.7}], \"note\": \"kort dansk forklaring på hvor tallene kommer fra\"}";

    private const string DevicePrompt = Common +
        "Apparatet bruger bare strøm mens det er tændt (køleskab, fryser, tv, router, varmepumpe ...). Find:\n" +
        "- hvad det er\n" +
        "- gennemsnitligt forbrug i watt mens det er tændt. Står der kWh/år på energimærket for et apparat " +
        "der altid er tændt, så er watt = kWh/år × 1000 / 8760.\n" +
        "- typisk antal timer om dagen det er tændt (24 for køl/frys/router), og hvilken time det typisk " +
        "tændes (fx 18 for tv), eller null hvis det er tændt hele dagen.\n" +
        "JSON: {\"name\": \"Køle-/fryseskab\", \"icon\": \"🧊\", \"model\": \"Samsung RB38\", \"watts\": 25, " +
        "\"hoursPerDay\": 24, \"usualFromHour\": null, \"note\": \"kort dansk forklaring på hvor tallene kommer fra\"}";

    public async Task<LookupResult> LookupAsync(string modelText, CancellationToken ct = default)
    {
        var (raw, sources, cost) = await AskAsync<RawMachine>(MachinePrompt, modelText, ct);
        var programs = (raw.Programs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Kwh is > 0 and < 100)
            .Select(p => new LookupProgram(p.Name!.Trim(), Math.Round(p.Kwh, 2)))
            .ToList();
        if (programs.Count == 0) throw new InvalidOperationException("Fandt ingen kWh-tal for den model.");
        return new LookupResult(
            Or(raw.Name, "Maskine"), Or(raw.Icon, "🔌"), Or(raw.Model, modelText.Trim()),
            raw.DurationHours is > 0 and <= 12 ? raw.DurationHours : 2,
            programs, raw.Note?.Trim() ?? "", sources, cost);
    }

    public async Task<DeviceLookupResult> LookupDeviceAsync(string modelText, CancellationToken ct = default)
    {
        var (raw, sources, cost) = await AskAsync<RawDevice>(DevicePrompt, modelText, ct);
        if (raw.Watts is not (> 0 and < 20000)) throw new InvalidOperationException("Fandt intet forbrug for den model.");
        var hours = raw.HoursPerDay is > 0 and <= 24 ? raw.HoursPerDay : 24;
        return new DeviceLookupResult(
            Or(raw.Name, "Enhed"), Or(raw.Icon, "🔌"), Or(raw.Model, modelText.Trim()),
            Math.Round(raw.Watts, 1), hours, hours >= 24 ? null : raw.UsualFromHour is >= 0 and <= 23 ? raw.UsualFromHour : null,
            raw.Note?.Trim() ?? "", sources, cost);
    }

    private async Task<(T Raw, List<string> Sources, double CostUsd)> AskAsync<T>(string system, string modelText, CancellationToken ct)
    {
        Take();
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Mangler Claude-nøgle på serveren.");
        var client = new AnthropicClient { ApiKey = apiKey };

        var res = await client.Messages.Create(new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 2000,
            System = system,
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            Messages = [new() { Role = Role.User, Content = $"Modelnummer: {modelText.Trim()}" }],
        }, ct);

        var sources = new List<string>();

        // Claude Opus 5 list price: $5 / $25 per million tokens.
        var cost = res.Usage.InputTokens * 5e-6 + res.Usage.OutputTokens * 25e-6;
        logger.LogInformation("Lookup {Model}: {In} in / {Out} out tokens, ~${Cost:0.000}",
            modelText, res.Usage.InputTokens, res.Usage.OutputTokens, cost);

        if (res.StopReason == "refusal") throw new InvalidOperationException("AI'en ville ikke svare på det modelnummer.");

        var text = string.Concat(res.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("Lookup for {Model} gave no JSON (stop {Stop}): {Text}", modelText, res.StopReason, text);
            throw new InvalidOperationException("Kunne ikke finde tal for den model. Prøv igen, eller skriv mærke + model.");
        }
        var raw = JsonSerializer.Deserialize<T>(text[start..(end + 1)], Json) ?? throw new InvalidOperationException("Tomt svar.");
        return (raw, sources.Distinct().Take(3).ToList(), cost);
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

    private static string Or(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();

    private sealed class RawMachine
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

    private sealed class RawDevice
    {
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Model { get; set; }
        public double Watts { get; set; }
        public double HoursPerDay { get; set; }
        public int? UsualFromHour { get; set; }
        public string? Note { get; set; }
    }
}
