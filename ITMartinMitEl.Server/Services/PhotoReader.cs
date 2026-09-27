using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

/// <summary>
/// MinElpris is camera-only: a photo of a rating plate or energy label sets up a machine or a
/// device, and a photo of a machine's display when it is started logs the run (a delay-start
/// "3h" on the display starts it three hours from now). Nobody types numbers.
/// One Claude call per photo, only when someone takes one, with a hard daily cap.
/// </summary>
public sealed class PhotoReader(IConfiguration config, ILogger<PhotoReader> logger)
{
    private const int MaxPerDay = 60;
    // Reading a label is text recognition, not research - Sonnet does it well at a fraction of Opus.
    private const string ModelId = "claude-sonnet-5";
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public sealed record Identified(Appliance? Machine, Device? Device, string Note);

    public sealed record StartRead(Guid? ApplianceId, int? Program, double DelayHours, double? DurationHours, string Note);

    private const string IdentifyPrompt =
        "Du ser et foto fra en dansk elpris-app: mærkeplade, energimærke, typeskilt eller selve apparatet. " +
        "Find ud af hvad apparatet er og hvor meget strøm det bruger. Læs mærke, model, energiklasse og tal " +
        "direkte fra billedet når de står der; ellers brug hvad du ved om modellen, og ellers typiske tal for " +
        "den slags apparat – og skriv i note hvilket af de tre det er.\n" +
        "kind = \"machine\" for noget der kører en tur ad gangen (vaskemaskine, opvaskemaskine, tørretumbler, ovn), " +
        "ellers \"device\" (køleskab, fryser, tv, router, varmepumpe, elvandvarmer ...).\n" +
        "Machine: kwhPerRun for det mest brugte program, durationHours for én tur, runsPerWeek typisk brug, " +
        "og programs med kWh for de almindelige programmer (vask: 30/40/60 °C, opvask: Eco/Normal/Intensiv).\n" +
        "Device: watts i gennemsnit mens det er tændt (for noget der altid er tændt: kWh/år × 1000 / 8760), " +
        "hoursPerDay, usualFromHour eller null.\n" +
        "kwhPerYear: årsforbrug ved normal brug (energimærkets tal hvis det står der).\n" +
        "newModel: en konkret ny energisparende model af samme slags der sælges i Danmark i dag, med dens årsforbrug " +
        "i kWh ved samme brug, og kilde (fx 'EU-energimærket (EPREL)').\n" +
        "Kan du ikke se et apparat på billedet, så sæt readable=false og forklar i note.\n" +
        "Svar med KUN ét JSON-objekt:\n" +
        "{\"readable\": true, \"kind\": \"machine\", \"name\": \"Opvaskemaskine\", \"icon\": \"🍽️\", \"brand\": \"Bosch\", " +
        "\"model\": \"SMS4HVI33E\", \"energyClass\": \"D\", \"kwhPerRun\": 0.92, \"durationHours\": 3.5, \"runsPerWeek\": 5, " +
        "\"programs\": [{\"name\": \"Eco 50\", \"kwh\": 0.92}], \"watts\": 0, \"hoursPerDay\": 0, \"usualFromHour\": null, " +
        "\"kwhPerYear\": 240, \"newModel\": {\"name\": \"Bosch SMD6TCX00E (A)\", \"kwhPerYear\": 151, \"source\": \"EU-energimærket (EPREL)\"}, " +
        "\"note\": \"kort dansk forklaring på hvor tallene kommer fra\"}";

    public async Task<Identified> IdentifyAsync(byte[] image, string mime, CancellationToken ct = default)
    {
        var raw = await AskAsync<RawIdentify>(IdentifyPrompt, "Hvilket apparat er det, og hvad bruger det?", image, mime, ct);
        if (raw.Readable == false) throw new InvalidOperationException(Or(raw.Note, "Kunne ikke se et apparat på billedet. Prøv et skarpere billede af mærkepladen."));

        var photo = new PhotoFacts
        {
            Brand = Or(raw.Brand, ""),
            EnergyClass = Or(raw.EnergyClass, ""),
            KwhPerYear = raw.KwhPerYear is > 0 and < 50000 ? Math.Round(raw.KwhPerYear, 0) : 0,
            Note = Or(raw.Note, ""),
            NewModelName = Or(raw.NewModel?.Name, ""),
            NewModelKwhPerYear = raw.NewModel?.KwhPerYear is > 0 and < 50000 ? Math.Round(raw.NewModel.KwhPerYear, 0) : 0,
            NewModelSource = Or(raw.NewModel?.Source, ""),
        };
        var model = string.Join(" ", new[] { raw.Brand, raw.Model }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

        if (raw.Kind == "machine")
        {
            var programs = (raw.Programs ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Kwh is > 0 and < 100)
                .Select(p => new ApplianceProgram { Name = p.Name!.Trim(), KwhPerRun = Math.Round(p.Kwh, 2) })
                .ToList();
            var kwh = raw.KwhPerRun is > 0 and < 100 ? raw.KwhPerRun : programs.FirstOrDefault()?.KwhPerRun ?? 0;
            if (kwh <= 0) throw new InvalidOperationException("Kunne ikke finde et forbrug for maskinen. Prøv et billede af energimærket.");
            var a = new Appliance
            {
                Name = Or(raw.Name, "Maskine"), Icon = Or(raw.Icon, "🔌"), Model = model,
                KwhPerRun = Math.Round(kwh, 2),
                DurationHours = raw.DurationHours is > 0 and <= 12 ? raw.DurationHours : 2,
                RunsPerWeek = raw.RunsPerWeek is > 0 and <= 30 ? raw.RunsPerWeek : 3,
                Programs = programs, Photo = photo,
            };
            a.SyncFromPrograms();
            if (photo.KwhPerYear <= 0) photo.KwhPerYear = Math.Round(a.KwhPerRun * a.RunsPerWeek * 52, 0);
            return new Identified(a, null, photo.Note);
        }

        if (raw.Watts is not (> 0 and < 20000)) throw new InvalidOperationException("Kunne ikke finde et forbrug for apparatet. Prøv et billede af typeskiltet.");
        var hours = raw.HoursPerDay is > 0 and <= 24 ? raw.HoursPerDay : 24;
        var d = new Device
        {
            Name = Or(raw.Name, "Apparat"), Icon = Or(raw.Icon, "🔌"), Model = model,
            Watts = Math.Round(raw.Watts, 1), HoursPerDay = hours,
            UsualFromHour = hours >= 24 ? null : raw.UsualFromHour is >= 0 and <= 23 ? raw.UsualFromHour : null,
            Photo = photo,
        };
        if (photo.KwhPerYear <= 0) photo.KwhPerYear = Math.Round(d.KwhPerYear, 0);
        return new Identified(null, d, photo.Note);
    }

    // The machine's own display when it is started: which machine, which program, and any delay-start.
    public async Task<StartRead> ReadStartAsync(byte[] image, string mime, IReadOnlyList<Appliance> machines, CancellationToken ct = default)
    {
        if (machines.Count == 0) throw new InvalidOperationException("Tilføj først maskinen med et billede af mærkepladen under Enheder.");
        var list = string.Join("\n", machines.Select((m, i) =>
            $"{i}: {m.Name} {m.Model}" + (m.Programs.Count > 0 ? " – programmer: " + string.Join(", ", m.Programs.Select((p, j) => $"{j}={p.Name}")) : "")));
        var prompt =
            "Du ser et foto af en maskines display eller panel, taget i det øjeblik brugeren starter den. " +
            "Afgør hvilken af husstandens maskiner det er, hvilket program der er valgt, og om den er sat til at starte senere " +
            "(fx '3h' eller 'Start om 3 t' = delayHours 3). Står der en resttid for selve programmet, så giv den i durationHours.\n" +
            "Husstandens maskiner:\n" + list + "\n" +
            "Svar med KUN ét JSON-objekt: {\"machine\": 0, \"program\": 1, \"delayHours\": 0, \"durationHours\": null, " +
            "\"note\": \"kort dansk: hvad du læste på displayet\"}. machine = null hvis det ikke ligner nogen af dem, program = null hvis det ikke kan ses.";
        var raw = await AskAsync<RawStart>(prompt, "Hvilken maskine starter jeg, og hvornår?", image, mime, ct);
        if (raw.Machine is not int mi || mi < 0 || mi >= machines.Count)
            throw new InvalidOperationException(Or(raw.Note, "Kunne ikke se hvilken maskine det er. Tag billedet af displayet."));
        var m = machines[mi];
        int? program = raw.Program is int pi && pi >= 0 && pi < m.Programs.Count ? pi : null;
        var delay = raw.DelayHours is > 0 and <= 24 ? raw.DelayHours : 0;
        double? duration = raw.DurationHours is > 0 and <= 12 ? raw.DurationHours : null;
        return new StartRead(m.Id, program, delay, duration, Or(raw.Note, ""));
    }

    private async Task<T> AskAsync<T>(string system, string question, byte[] image, string mime, CancellationToken ct)
    {
        Take();
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Mangler Claude-nøgle på serveren.");
        var client = new AnthropicClient { ApiKey = apiKey };
        var res = await client.Messages.Create(new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 1500,
            System = system,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(image), MediaType = mime } },
                        new TextBlockParam { Text = question },
                    },
                },
            ],
        }, ct);

        // Claude Sonnet 5 list price: $3 / $15 per million tokens.
        var cost = res.Usage.InputTokens * 3e-6 + res.Usage.OutputTokens * 15e-6;
        logger.LogInformation("Photo read: {In} in / {Out} out tokens, ~${Cost:0.000}", res.Usage.InputTokens, res.Usage.OutputTokens, cost);
        if (res.StopReason == "refusal") throw new InvalidOperationException("AI'en ville ikke læse billedet.");

        var text = string.Concat(res.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("Photo read gave no JSON (stop {Stop}): {Text}", res.StopReason, text);
            throw new InvalidOperationException("Kunne ikke læse billedet. Prøv igen med et skarpere billede.");
        }
        return JsonSerializer.Deserialize<T>(text[start..(end + 1)], Json) ?? throw new InvalidOperationException("Tomt svar.");
    }

    private void Take()
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= MaxPerDay) throw new InvalidOperationException("Dagens billeder er brugt op. Prøv igen i morgen.");
            _used++;
        }
    }

    private static string Or(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();

    private sealed class RawIdentify
    {
        public bool? Readable { get; set; }
        public string? Kind { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? EnergyClass { get; set; }
        public double KwhPerRun { get; set; }
        public double DurationHours { get; set; }
        public double RunsPerWeek { get; set; }
        public List<RawProgram>? Programs { get; set; }
        public double Watts { get; set; }
        public double HoursPerDay { get; set; }
        public int? UsualFromHour { get; set; }
        public double KwhPerYear { get; set; }
        public RawNew? NewModel { get; set; }
        public string? Note { get; set; }
    }

    private sealed class RawProgram
    {
        public string? Name { get; set; }
        public double Kwh { get; set; }
    }

    private sealed class RawNew
    {
        public string? Name { get; set; }
        public double KwhPerYear { get; set; }
        public string? Source { get; set; }
    }

    private sealed class RawStart
    {
        public int? Machine { get; set; }
        public int? Program { get; set; }
        public double DelayHours { get; set; }
        public double? DurationHours { get; set; }
        public string? Note { get; set; }
    }
}
