using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinForloebet.Server.Data.Entities;

namespace ITMartinForloebet.Server.Services;

/// <summary>The two AI roles from the spec, and nothing else: (1) rewrite the user's
/// informal message into the text that gets published, (2) analyse what is written.
/// Every call goes through <see cref="AiBudget"/>.</summary>
public sealed class ForloebetAi
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private const string Grundregler =
        "Du skriver for Forløbet, et dansk opsamlingssted hvor borgere gengiver, hvad de oplevede i en " +
        "proces over for en stor spiller (trafikselskab, forsikring, kommune, bank). Siden stempler ikke " +
        "enkeltpersoner og tager ikke stilling; fokus er på lange, rigide processer hvor borgeren står alene.\n" +
        "Redaktionel regel: Beskriv hændelsen - ikke personen.\n" +
        "Sprog: nøgternt dansk. Skriv \"Movia oplyste ...\" - aldrig \"Movia lyver\". Skriv \"dokumentationen viser ...\" " +
        "kun når der er et konkret grundlag. Skeln mellem hvad der er dokumenteret, hvad der er gengivet fra en " +
        "anden part, og hvad der er borgerens egen oplevelse.\n" +
        "Fjern ALTID: navne på medarbejdere/sagsbehandlere/kontrollører (skriv 'en sagsbehandler'), CPR-numre, " +
        "adresser, telefonnumre, e-mails, navne på andre privatpersoner, beskyldninger og skældsord. Organisationer " +
        "(Movia, Keolis, Ankenævnet, politiet) må nævnes. Sagsnumre må beholdes.";

    private readonly AnthropicClient _client;
    private readonly AiBudget _budget;
    private readonly Knowledge _knowledge;
    private readonly ILogger<ForloebetAi> _logger;

    public ForloebetAi(IConfiguration config, AiBudget budget, Knowledge knowledge, ILogger<ForloebetAi> logger)
    {
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Missing Claude:ApiKey");
        _client = new AnthropicClient { ApiKey = apiKey };
        _budget = budget;
        _knowledge = knowledge;
        _logger = logger;
    }

    // ---------------------------------------------------------------- rewrite

    private static readonly Tool OmskrivTool = new()
    {
        Name = "omskriv",
        Description = "Den omskrevne, publicerbare tekst plus metadata",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["tekst"] = P("string", "Den omskrevne tekst, 1-5 sætninger, nøgtern, uden personoplysninger"),
                ["part"] = P("string", "Hvem handlede: Borger, Modpart eller Anden"),
                ["dato"] = P("string", "Dato for hændelsen som yyyy-MM-dd hvis teksten nævner en, ellers tom streng"),
                ["citat"] = P("string", "Den afgørende sætning ordret, hvis beskeden indeholder et citat fra modparten; ellers tom"),
                ["fjernet"] = P("string", "Kort til borgeren: hvad du udelod og hvorfor (fx 'Sagsbehandlerens navn er udeladt'). Tom hvis intet"),
                ["titel"] = P("string", "KUN ved nyt forløb: kort titel, fx 'Kontrolafgift - var ikke i bussen'. Ellers tom"),
                ["modpart"] = P("string", "KUN ved nyt forløb: organisationen, fx 'Movia'. Ellers tom"),
                ["omraade"] = P("string", "KUN ved nyt forløb: område, fx 'Kontrolafgift', 'Forsikring', 'Kommune'. Ellers tom"),
            },
            Required = ["tekst", "part", "dato", "citat", "fjernet", "titel", "modpart", "omraade"]
        }
    };

    public async Task<Omskrivning> OmskrivAsync(string raw, Forloeb? existing, CancellationToken ct = default)
    {
        _budget.Take("omskriv");
        var context = existing is null
            ? "Dette er den FØRSTE besked i et nyt forløb. Udfyld titel, modpart og område."
            : $"Forløbet findes allerede: '{existing.Titel}' mod {existing.Modpart} ({existing.Omraade}). " +
              $"Seneste trin:\n{Tidslinje(existing, 5)}\nDette er et NYT trin. Lad titel/modpart/område være tomme.";

        var res = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 1024,
            System = Grundregler + "\n\nOpgave: Borgeren har skrevet kort og uformelt. Omskriv til den tekst, der " +
                     "offentliggøres. Bevar indholdet, fjern alt personhenførbart og alt der kan være injurierende. " +
                     "Tilføj ikke fakta, borgeren ikke har skrevet.",
            Tools = [OmskrivTool],
            ToolChoice = new ToolChoiceTool { Name = "omskriv" },
            Messages = [new() { Role = Role.User, Content = $"{context}\n\nBorgerens besked:\n\"\"\"\n{raw}\n\"\"\"" }]
        }, ct);

        var o = Parse<Omskrivning>(res) ?? new Omskrivning { Tekst = raw };
        if (string.IsNullOrWhiteSpace(o.Dato)) o.Dato = null;
        return o;
    }

    // ------------------------------------------------------- document -> steps

    private static readonly Tool TrinTool = new()
    {
        Name = "trin",
        Description = "Trin til tidslinjen udledt af et dokument",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["trin"] = JsonDocument.Parse("""
                    {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "dato":  { "type": "string", "description": "yyyy-MM-dd" },
                          "part":  { "type": "string", "description": "Borger, Modpart eller Anden" },
                          "tekst": { "type": "string", "description": "Hvad blev oplyst/gjort, nøgternt, uden personoplysninger, 1-3 sætninger" },
                          "citat": { "type": "string", "description": "Den afgørende sætning ordret, hvis der er en; ellers tom" }
                        },
                        "required": ["dato", "part", "tekst", "citat"]
                      }
                    }
                    """).RootElement
            },
            Required = ["trin"]
        }
    };

    public async Task<List<TrinForslag>> TrinFraDokumentAsync(Forloeb f, string? text, byte[]? image, string? imageMime, byte[]? pdf, CancellationToken ct = default)
    {
        _budget.Take("dokument");
        var content = new List<ContentBlockParam>();
        if (image is not null && imageMime is not null)
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(image), MediaType = imageMime } });
        if (pdf is not null)
            content.Add(new DocumentBlockParam { Source = new Base64PdfSource { Data = Convert.ToBase64String(pdf) } });
        var prompt = $"Forløb: '{f.Titel}' mod {f.Modpart}. Eksisterende trin:\n{Tidslinje(f, 20)}\n\n" +
                     "Læs dokumentet (mail, brev, afgørelse) og foreslå de trin, det tilføjer til tidslinjen: hver " +
                     "dato hvor nogen gjorde eller oplyste noget. Spring trin over, der allerede findes. Citér den " +
                     "afgørende sætning ordret, når en part oplyser noget, der kan få betydning senere (fx at noget " +
                     "er 'sikret', en frist, et krav).";
        if (!string.IsNullOrWhiteSpace(text)) prompt += $"\n\nDokument:\n\"\"\"\n{Trim(text, 30000)}\n\"\"\"";
        content.Add(new TextBlockParam { Text = prompt });

        var res = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 2048,
            System = Grundregler,
            Tools = [TrinTool],
            ToolChoice = new ToolChoiceTool { Name = "trin" },
            Messages = [new() { Role = Role.User, Content = content }]
        }, ct);

        var json = ToolJson(res);
        if (json is null) return [];
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("trin", out var arr)
            ? JsonSerializer.Deserialize<List<TrinForslag>>(arr.GetRawText(), Json) ?? []
            : [];
    }

    // --------------------------------------------------------------- analysis

    private static readonly Tool AnalyseTool = new()
    {
        Name = "analyse",
        Description = "AI-analyse af forløbet ud fra de seks spørgsmål",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["hvadHandledeDetOm"] = P("string", "1. Hvad handlede sagen om? Én-to sætninger, nøgternt."),
                ["hvadSkulleManGoere"] = P("string", "2. Hvad skulle man gøre som borger? De skridt der reelt flyttede/flytter sagen. Punktliste med '- '."),
                ["hvadKraeves"] = P("string", "3. Hvad kræves der, at borgeren gør? Frister, formkrav, dokumentation man ER forpligtet til. Punktliste."),
                ["jaOgNejTil"] = P("string", "4. Hvad kan man tillade sig at sige ja og nej til? Det modparten beder om, som man ikke er forpligtet til, og det man bør sige ja til. Punktliste med 'Nej:'/'Ja:'."),
                ["unoedigTid"] = P("string", "5. Hvor tog processen unødigt lang tid og fagpersoners tid? Trin der kunne kortes, hvilke instanser blev involveret, hvem betalte tiden. Punktliste."),
                ["love"] = P("string", "6. Relevante love og regler med præcise henvisninger fra videnbasen. Punktliste. Kun hvad der faktisk rammer sagen."),
                ["udkast"] = JsonDocument.Parse("""
                    { "type": "array", "description": "0-2 færdige tekstudkast borgeren kan bruge nu (klage, indsigtsanmodning, svar). Kun hvis forløbet er åbent og et udkast er oplagt.",
                      "items": { "type": "object", "properties": { "titel": {"type":"string"}, "tekst": {"type":"string"} }, "required": ["titel","tekst"] } }
                    """).RootElement,
                ["lignende"] = JsonDocument.Parse("""
                    { "type": "array", "description": "Lignende afsluttede forløb fra listen, med slug og én linje om hvad de viste. Tom hvis ingen passer.",
                      "items": { "type": "object", "properties": { "slug": {"type":"string"}, "linje": {"type":"string"} }, "required": ["slug","linje"] } }
                    """).RootElement,
                ["resume"] = P("string", "3-4 linjer til forsiden: hvad skete, hvad blev gjort, hvordan det står/endte, læring. Ingen punktliste."),
            },
            Required = ["hvadHandledeDetOm", "hvadSkulleManGoere", "hvadKraeves", "jaOgNejTil", "unoedigTid", "love", "udkast", "lignende", "resume"]
        }
    };

    public async Task<Analyse> AnalyserAsync(Forloeb f, IReadOnlyList<Forloeb> delteAfsluttede, CancellationToken ct = default)
    {
        _budget.Take("analyse");
        var andre = delteAfsluttede.Where(x => x.Id != f.Id).Take(30)
            .Select(x => $"- slug={x.Slug}: {x.Modpart} · {x.Titel} · {x.Uger} uger · {UdfaldTekst(x.Udfald)}. Læring: {Trim(x.Laering, 300)}");
        var sb = new StringBuilder();
        sb.AppendLine($"Forløb: '{f.Titel}' mod {f.Modpart}, område {f.Omraade}. Startet {f.StartedAt:yyyy-MM-dd}. " +
                      (f.Lukket ? $"Afsluttet {f.ClosedAt:yyyy-MM-dd}: {UdfaldTekst(f.Udfald)}. {f.UdfaldTekst}\nLæring: {f.Laering}" : "Stadig åbent."));
        sb.AppendLine("\nTidslinje:");
        sb.AppendLine(Tidslinje(f, 60));
        sb.AppendLine("\nAndre delte, afsluttede forløb:");
        sb.AppendLine(andre.Any() ? string.Join("\n", andre) : "(ingen)");

        var res = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 4096,
            System = Grundregler +
                     "\n\nOpgave: Lav en AI-analyse af forløbet ud fra de seks spørgsmål. Analysen er gengivelse af, hvad " +
                     "reglerne siger og hvad andre forløb viste - ikke rådgivning, men den skal være lige så konkret og " +
                     "brugbar som en grundig gennemgang fra en kyndig ven: sig præcist hvad borgeren ikke er forpligtet " +
                     "til (fx at bevise sit fravær - modparten skal dokumentere identifikationen), og hvad der reelt " +
                     "flytter sagen (bed om dokumentationen; indsigt efter GDPR art. 15). Brug kun henvisninger, der " +
                     "står i videnbasen eller er almen viden; opfind ikke paragraffer.\n\n" + _knowledge.All(),
            Tools = [AnalyseTool],
            ToolChoice = new ToolChoiceTool { Name = "analyse" },
            Messages = [new() { Role = Role.User, Content = sb.ToString() }]
        }, ct);

        return Parse<Analyse>(res) ?? new Analyse();
    }

    // --------------------------------------------------------------- learning

    private static readonly Tool LaeringTool = new()
    {
        Name = "laering",
        Description = "Forslag til 'Hvad lærte jeg'",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["punkter"] = JsonDocument.Parse("""{ "type": "array", "items": { "type": "string" }, "description": "Præcis 3 korte punkter i borgerens egen stemme ('Bed om dokumentationen før du svarer')" }""").RootElement
            },
            Required = ["punkter"]
        }
    };

    public async Task<List<string>> ForeslaaLaeringAsync(Forloeb f, Udfald udfald, string udfaldTekst, CancellationToken ct = default)
    {
        _budget.Take("laering");
        var res = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 512,
            System = Grundregler,
            Tools = [LaeringTool],
            ToolChoice = new ToolChoiceTool { Name = "laering" },
            Messages = [new() { Role = Role.User, Content =
                $"Forløb '{f.Titel}' mod {f.Modpart}. Tidslinje:\n{Tidslinje(f, 60)}\n\nUdfald: {UdfaldTekst(udfald)}. {udfaldTekst}\n\n" +
                "Foreslå 3 punkter til 'Hvad lærte jeg' - konkrete, brugbare for den næste i samme situation." }]
        }, ct);
        var json = ToolJson(res);
        if (json is null) return [];
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("punkter", out var arr)
            ? arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).Take(3).ToList()
            : [];
    }

    // --------------------------------------------------------------- headline

    public async Task<string> OverskriftAsync(IReadOnlyList<Forloeb> delte, CancellationToken ct = default)
    {
        _budget.Take("overskrift");
        var lines = delte.Take(60).Select(x => $"- {x.Modpart} · {x.Omraade} · {x.Titel} · {x.Uger} uger · {(x.Lukket ? UdfaldTekst(x.Udfald) : "åbent")}");
        var res = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 120,
            System = Grundregler + "\n\nSkriv ÉN linje (max 110 tegn) der samler op på forløbene på forsiden. Tal og mønster, ingen dom, ingen punktum til sidst. Eksempel: '14 forløb om kontrolafgifter - 9 endte med frafald, typisk efter at borgeren bad om dokumentationen'. Svar kun med linjen.",
            Messages = [new() { Role = Role.User, Content = string.Join("\n", lines) }]
        }, ct);
        var text = string.Join("", res.Content.Select(c => c.TryPickText(out var t) ? t.Text : "")).Trim().Trim('"');
        return text.Length > 140 ? text[..140] : text;
    }

    // ---------------------------------------------------------------- helpers

    public static string UdfaldTekst(Udfald u) => u switch
    {
        Udfald.Aabent => "åbent",
        Udfald.Frafaldet => "frafaldet",
        Udfald.Betalt => "betalt",
        Udfald.Delvist => "delvist",
        Udfald.Afvist => "afvist",
        _ => "andet"
    };

    private static string Tidslinje(Forloeb f, int max) =>
        string.Join("\n", f.Trin.OrderBy(t => t.At).TakeLast(max)
            .Select(t => $"- {t.At:yyyy-MM-dd} [{t.Part}] {t.Tekst}" + (t.Citat.Length > 0 ? $" Citat: \"{t.Citat}\"" : "")));

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + " …";

    private static JsonElement P(string type, string description) =>
        JsonDocument.Parse($$"""{ "type": "{{type}}", "description": {{JsonSerializer.Serialize(description)}} }""").RootElement;

    private static string? ToolJson(Message res)
    {
        foreach (var block in res.Content)
            if (block.TryPickToolUse(out var tu)) return JsonSerializer.Serialize(tu.Input);
        return null;
    }

    private T? Parse<T>(Message res) where T : class
    {
        var json = ToolJson(res);
        if (json is null) { _logger.LogWarning("No tool_use block in Claude response"); return null; }
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (Exception ex) { _logger.LogWarning(ex, "Bad tool json: {Json}", json); return null; }
    }
}
