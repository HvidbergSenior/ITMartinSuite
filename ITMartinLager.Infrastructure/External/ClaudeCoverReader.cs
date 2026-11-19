using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Models.Messages.Batches;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.External;

// One photo of a PILE (10-20 covers side by side) -> one Claude call that reads every item. Never one call per item:
// 100,000 items is ~7,000 photos this way (CLAUDE.md cost rules). Cheap model by default (Lager:AiModel), and a hard
// daily cap (Lager:MaxAiCallsPerDay) counted in the database, so a restart or a loop cannot run up the bill.
public sealed class ClaudeCoverReader(IConfiguration cfg, IDbContextFactory<LagerDb> dbf, ILogger<ClaudeCoverReader> log) : ICoverReader
{
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim CapGate = new(1, 1);

    private string Model => cfg["Lager:AiModel"] ?? "claude-haiku-4-5";
    // Which model reads a pile photo. The cheap one by default; a stronger one (Lager__PileModel, e.g.
    // claude-sonnet-5-5) reads small issue numbers better (2026-10-12 test: Haiku dropped "Nr. 22", "#3", "No. 2").
    private string PileModel => cfg["Lager:PileModel"] is { Length: > 0 } m ? m : Model;
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
                          "series":     { "type": "string", "description": "Series printed on the cover, e.g. 'Anders And & Co.', 'Superserien', 'Edderkoppen', 'The Amazing Spider-Man'. Never just the publisher." },
                          "publisher":  { "type": "string", "description": "Publisher/label if printed: 'Marvel', 'DC', 'Semic', 'Interpresse', 'Image'. Empty if not seen." },
                          "number":     { "type": "string", "description": "Issue/volume number as printed, e.g. '12', '22', '3'. Never a year." },
                          "year":       { "type": ["integer", "null"] },
                          "platform":   { "type": "string", "description": "Console games only: 'PlayStation 2', 'Nintendo 64' ..." },
                          "condition":  { "type": "string", "enum": {{JsonSerializer.Serialize(Conditions.All)}} },
                          "usage":      { "type": "string", "enum": {{JsonSerializer.Serialize(Usages.All)}},
                                          "description": "Ny ONLY when it is clearly unused and never sold: sealed in plastic, shop sticker, price label of a new-goods shop. Else Brugt." },
                          "language":   { "type": "string", "enum": {{JsonSerializer.Serialize(Languages.All)}},
                                          "description": "The language of the item's own text (title, cover). Dansk, Engelsk or Andet sprog." },
                          "quantity":   { "type": "integer", "minimum": 1 },
                          "confidence": { "type": "number", "description": "0-1: how sure the title/number reading is" },
                          "note":       { "type": "string", "description": "Short note if something is unreadable or damaged" },
                          "sticker_kr": { "type": ["integer", "null"], "description": "A price written ON the item itself: a price sticker, label, stamp or pencil mark (e.g. '69', '69,-', 'kr. 25'). Only the number in kroner. null when none is seen. Never the price printed by the publisher as part of the cover design." },
                          "sticker_area": { "type": "array", "items": { "type": "number" }, "minItems": 4, "maxItems": 4,
                                            "description": "Only when sticker_kr is set: where that sticker/label/pencil price lies in IMAGE 1, [left, top, right, bottom] as parts of the width/height, 0-1, tight around it. It is covered on the item's picture." },
                          "edition_hint": { "type": "string", "enum": {{JsonSerializer.Serialize(EditionHints.All)}},
                                            "description": "Books only: what the cover/spine itself shows about the edition, else empty" },
                          "kolofon":    { "type": "boolean",
                                          "description": "true when the photo shows an INSIDE page of ONE book (the colophon behind the title page, the title page, the last page with printing details) or its BACK cover - not its front cover or spine" },
                          "edition":    { "type": "string", "description": "Only for a kolofon page: edition and printing as printed, e.g. '1. udgave, 1. oplag', '3. oplag 1972'. Empty if not printed." },
                          "isbn":       { "type": "string", "description": "Only for a kolofon page: the ISBN as printed, else empty" },
                          "area":       { "type": "array", "items": { "type": "number" }, "minItems": 4, "maxItems": 4,
                                          "description": "Where the item lies in IMAGE 1 (the whole pile): [left, top, right, bottom] as parts of the width/height, 0-1. Tight around this item only." }
                        },
                        "required": ["kind", "title", "condition", "quantity", "confidence"]
                      }
                    }
                    """).RootElement,
                // "Tag igen" (user 2026-11-15: "if the picture is rotated and so on it should be taken again") - same call, no extra cost.
                ["retake"] = JsonDocument.Parse("""
                    { "type": "string",
                      "description": "Only when the PHOTO itself is bad: a short Danish reason it must be taken again - 'drejet' (sideways or upside down), 'uskarpt', 'for langt væk', 'skåret af', 'for mørkt', 'blænder'. Empty when the photo is fine, even if a single item is hard to read." }
                    """).RootElement,
            },
            Required = ["items"],
        },
    };

    private const string System = """
        Du registrerer brugte varer til salg i en dansk butik: Anders And-blade, Anders And Jumbobøger, tegneserier,
        magasiner, bøger, CD'er, DVD'er, Blu-rays, konsolspil og Magic: The Gathering-kort.
        Billedet viser en bunke varer lagt ud side om side - ELLER en række CD'er, DVD'er, LP'er eller bøger, der står
        med ryggen mod kameraet (i en kasse eller på en hylde). Find HVER vare og kald report_items.
        - Rygge: hver smal stribe er én vare. Teksten på ryggen står ofte på langs (drejet 90 grader) - læs den alligevel.
          Typisk "KUNSTNER · ALBUM" eller "ALBUM - KUNSTNER" på en CD/LP, "FORFATTER  TITEL  FORLAG" på en bog, filmens
          titel på en DVD. Logoet eller formen afgør kind (smal plastikryg = CD, høj æske = DVD, tynd pap = LP).
          Tæl striberne, så ingen ryg springes over; en ryg, der ikke kan læses, kommer med med confidence lavt og en note.
        - Læs titel, nummer og år præcist som de står på forsiden/ryggen. Gæt aldrig et nummer - lad det være tomt,
          og sæt confidence lavt, hvis det ikke kan læses.
        - Skriv BOGSTAV FOR BOGSTAV som trykt: æ, ø og å beholdes ("Søuhyret", ikke "Souhyret"), og særlige stavemåder
          rettes aldrig ("Zap Comix", ikke "Comics"; "Storm Petersen", ikke et ord du kender bedre). Brug nærbillederne.
        - number = hæftets/albummets nummer ("Nr. 22", "No. 2", "#3"). Et årstal er ALDRIG et nummer - det skal i year.
          "29. Aargang 1941" = number "29", year 1941.
        - series = seriens navn, der står på forsiden: "Superserien", "Anders And & Co.", "Gyldenspjæt", "Edderkoppen",
          "The Amazing Spider-Man", "Batman". Forlaget (Marvel, DC, Semic, Interpresse, Image, Dynamite) er ALDRIG serien -
          det skal i publisher. title = hæftets egen titel ("Søuhyret fra Volapyk"). Står der også en undertitel eller
          et særnummer ("Med på den værste", "Two Day Special", "Grim Hunt Part 3"), så skriv den med i title efter
          " – ": "Kasket Karl – Med på den værste", "The Walking Dead – Two Day Special".
        - Titler læses hele - forkort aldrig med "..." eller "…". Er en del skjult, så skriv det, der kan ses, uden prikker.
        - kind - vælg ud fra hvad det ER:
          * "Anders And-blad": ugebladet Anders And & Co.: serie "Anders And & Co.", nummer og år fra forsiden.
          * "Jumbobog": KUN når der står "Jumbobog" på den. Nummer fra ryggen/forsiden.
          * "Superhelteblad": superheltehæfter - Marvel og DC (X-Men, Spider-Man, Batman, Superman ...), også de danske
            udgaver (Fantastic Four, Superman-bladet). Nummer fra forsiden.
          * "Tegneserie": alle andre tegneserier og tegneseriealbum - Superserien, Storm P.-album, Gyldenspjæt,
            Tintin, Asterix, Lucky Luke, julehæfter med tegneserier, undergrund som Zap Comix.
          * "Bog": bøger med tekst, også fagbøger og instruktionsbøger (musik, hobby, sprog), selv om der følger en CD med.
          * "Magasin": blade med artikler (ikke tegneserier).
        - CD, "Vinyl" (LP/single) og "Kassettebånd": titel = albummets navn, artist = kunstneren eller gruppen, som den
          står på forsiden - ikke alle de medvirkende musikere ("The Klaus Suonsaari Quintet", ikke fem navne).
        - Film: "DVD", "Blu-ray" eller "VHS" ud fra æsken. Konsolspil: platform (fx "PlayStation 2").
        - Spil i en CD-æske er IKKE en CD: står der et spilfirma på ryggen (Eidos, EA, Ubisoft, Activision, Blizzard,
          Sierra, Microsoft Games, "PC CD-ROM"), så er kind "Konsolspil" og platform "PC" (eller konsollen).
        - Tomme diske er ikke varer: blanke CD-R/DVD-R/RW (Fujifilm, TDK, Verbatim, Maxell, "CD-Recordable", "650MB",
          "700MB") og hjemmebrændte diske uden trykt omslag kommer IKKE med på listen.
        - Magic-kort: titel = kortets navn, serie = udgivelsen hvis den kan ses.
        - Stand ud fra hvad der kan ses (rifter, fold, slid, mangler): Som ny, Meget god, God, Slidt, Defekt.
        - usage: "Ny" kun når tingen tydeligt er ny og aldrig solgt (forseglet i plastik, butikkens mærkat). Ellers "Brugt".
        - language: sproget på varens egen tekst (titel, omslag): Dansk, Engelsk eller Andet sprog.
        - Ens eksemplarer = én post med quantity.
        - area: hvor varen ligger på billede 1 (hele bunken), [venstre, top, højre, bund] som brøkdele 0-1 af bredde og
          højde - tæt om netop den vare, så den kan klippes ud som varens eget billede. En ryg = den smalle stribe.
        - Bøger: titel og forfatter præcist som trykt (med æ/ø/å). Kan kun en del læses, så skriv det, der kan læses, og
          sæt confidence lavt - opfind aldrig en titel, der "lyder rigtig".
        - Bøger: edition_hint ud fra hvad omslaget/ryggen SELV viser: "Bogklub" (Bogklubben, Lademann-bogklub, Book Club
          Edition o.l.), "Billigbog" (paperback-genudgivelse, "Gyldendals Bekkasinbøger", "Lommebog", pris på forsiden fra et
          senere oplag), "Mulig 1. udgave" (originalt forlagsomslag med smudsomslag, gammelt forlagslogo, "1. udgave"
          trykt på omslaget). Ellers tomt. Gæt ikke - tomt er bedre end forkert.
        - Kolofon: viser billedet en INDERSIDE af én bog (kolofonen bag titelbladet, titelbladet eller sidste side med
          tryk-oplysninger), så er det ÉN post med kolofon = true: titel og forfatter som de står på siden, edition =
          udgave og oplag ordret ("1. udgave, 1. oplag", "3. oplag 1972"), isbn som trykt, year = årstallet for udgaven,
          publisher = forlaget. Ellers kolofon = false og edition/isbn tomme. Gæt aldrig en udgave.
        - Kolofonen står tit UDEN titel og uden udgave (ældre bøger: "© Tove Ditlevsen, 1968", omslag, trykkeri). Rapportér den
          ALLIGEVEL med kolofon = true: artist = navnet efter ©, year = ©-årstallet, publisher/trykkeri i note, og edition ordret -
          står der intet om udgave eller oplag, så edition = "Ingen udgave/oplag nævnt (© <år>)". title tom, hvis den ikke står der.
        - En BAGSIDE af en bog (ingen titel på forsiden at se, tekst om bogen, stregkode/ISBN) er IKKE en ny vare: kolofon = true,
          isbn fra stregkoden, titel og forfatter kun hvis de står der. Opfind aldrig en titel ud fra teksten på bagsiden.
        """;

    public async Task<int> CallsTodayAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Now);
        return (await db.AiDays.FindAsync(today))?.Calls ?? 0;
    }

    // Prompt caching (user 2026-11-15: "add caching"): the tools and the instructions are the same on every call, so they are
    // marked for the cache - after the first photo they cost about a tenth (Sonnet 5.5 reads $0.10/MTok). Photos come in bursts
    // (a Shortcut session, a restart), so the 5-minute cache is the right one. Below the model's minimum it simply does not cache.
    private static MessageCreateParamsSystem Cached(string system) =>
        new List<TextBlockParam> { new() { Text = system, CacheControl = new CacheControlEphemeral() } };

    // One call per photo: the whole pile plus up to four close-ups of it in the same message (never a call per close-up).
    public async Task<PileReading> ReadPileAsync(byte[] jpeg, IReadOnlyList<byte[]> closeUps, string hint, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");

        await ReserveCallAsync(ct);
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var content = PileContent(jpeg, closeUps, hint);
        ToolUseBlock? tool;
        Message response;
        if (PileModel == Model)
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 12000,
                System = Cached(System),
                Tools = [ReportTool],
                ToolChoice = new ToolChoiceTool { Name = "report_items" },
                Messages = [new() { Role = Role.User, Content = content }],
            }, ct);
            await CountTokensAsync(response, ct);
            tool = response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault();
        }
        else
        {
            content.Add(new TextBlockParam { Text = "Svar ved at kalde report_items." });
            (tool, var r) = await AccurateToolCallAsync(System, ReportTool, content, PileModel, ct);
            response = r!;
        }
        if (tool is null) throw new InvalidOperationException("AI'en svarede ikke med en liste. Prøv et nyt billede.");
        var reading = ParsePile(tool);
        log.LogInformation("Pile read by {Model}: {Count} items, {In}/{Out} tokens, cache read {CacheRead} / written {CacheWrite}", response.Model, reading.Items.Count, response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CacheReadInputTokens, response.Usage.CacheCreationInputTokens);
        return reading;
    }

    // The photo, its close-ups and the ask - the same for a read at once and for a batch.
    private static List<ContentBlockParam> PileContent(byte[] jpeg, IReadOnlyList<byte[]> closeUps, string hint)
    {
        var text = "Registrér alle varer på billedet." + (string.IsNullOrWhiteSpace(hint) ? "" : $" Det er mest: {hint}.")
            + (closeUps.Count == 0 ? "" :
                $" Billede 1 er hele bunken. Billede 2-{closeUps.Count + 1} er nærbilleder af SAMME bunke i høj opløsning"
                + " (øverst til venstre, øverst til højre, nederst til venstre, nederst til højre - de overlapper lidt)."
                + " Brug hele billedet til at tælle varerne og nærbillederne til at læse titel, forfatter og nummer bogstav for bogstav."
                + " En vare, der ses på flere billeder, er stadig ÉN vare.");
        var content = new List<ContentBlockParam>
        {
            new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(jpeg), MediaType = "image/jpeg" } },
        };
        foreach (var c in closeUps)
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(c), MediaType = "image/jpeg" } });
        content.Add(new TextBlockParam { Text = text });
        return content;
    }

    private static PileReading ParsePile(ToolUseBlock tool)
    {
        var parsed = JsonSerializer.Deserialize<Report>(JsonSerializer.Serialize(tool.Input), Json) ?? new Report();
        return new PileReading(parsed.Items.Select(Clean).Where(f => f.Title.Length > 0 || (f.Kolofon && (f.Artist.Length > 0 || f.Barcode.Length > 0))).ToList(),
            (parsed.Retake ?? "").Trim());
    }

    // ---- The Batch API (user 2026-11-15: "take a lot of pictures. And then api on as many as possible fewer calls") ----
    // Half price; the answers come within the hour as a rule. Each photo still counts against the daily cap when it is sent.
    public bool Batch => cfg.GetValue("Lager:AiBatch", true);

    public async Task<string> SubmitPileBatchAsync(IReadOnlyList<PileRequest> piles, CancellationToken ct)
    {
        foreach (var _ in piles) await ReserveCallAsync(ct);
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var forced = PileModel == Model;   // the cheap model takes a forced call; Sonnet/Opus 5.5 do not, so the ask says it
        var requests = piles.Select(p =>
        {
            var content = PileContent(p.Jpeg, p.CloseUps, p.Hint);
            if (!forced) content.Add(new TextBlockParam { Text = "Svar ved at kalde report_items." });
            return new Request
            {
                CustomID = p.Id,
                Params = new()
                {
                    Model = PileModel,
                    MaxTokens = 12000,
                    System = new List<TextBlockParam> { new() { Text = System, CacheControl = new CacheControlEphemeral() } },
                    Tools = [ReportTool],
                    ToolChoice = forced ? new ToolChoiceTool { Name = "report_items" } : new ToolChoiceAuto(),
                    OutputConfig = forced ? null : new OutputConfig { Effort = Effort.Low },
                    Messages = [new() { Role = Role.User, Content = content }],
                },
            };
        }).ToList();
        var batch = await client.Messages.Batches.Create(new BatchCreateParams { Requests = requests }, ct);
        log.LogInformation("Pile batch {Id} sent: {Count} photos", batch.ID, piles.Count);
        return batch.ID;
    }

    public async Task<Dictionary<string, PileReading>?> PileBatchResultsAsync(string batchId, CancellationToken ct)
    {
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var batch = await client.Messages.Batches.Retrieve(batchId, cancellationToken: ct);
        if (batch.ProcessingStatus != "ended") return null;
        var answers = new Dictionary<string, PileReading>();
        long tokensIn = 0, tokensOut = 0;
        await foreach (var r in client.Messages.Batches.ResultsStreaming(batchId, cancellationToken: ct))
        {
            if (!r.Result.TryPickSucceeded(out var ok)) continue;   // errored/canceled/expired - sent again with the next batch
            tokensIn += ok.Message.Usage.InputTokens; tokensOut += ok.Message.Usage.OutputTokens;
            if (ok.Message.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault() is { } tool)
                answers[r.CustomID] = ParsePile(tool);
        }
        await CountBatchTokensAsync(tokensIn, tokensOut, ct);
        log.LogInformation("Pile batch {Id} answered: {Count} photos, {In}/{Out} tokens (half price)", batchId, answers.Count, tokensIn, tokensOut);
        return answers;
    }

    private async Task CountBatchTokensAsync(long tokensIn, long tokensOut, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var day = await db.AiDays.FindAsync([today], ct);
        if (day is null) db.AiDays.Add(day = new AiDay { Day = today });
        day.InputTokens += tokensIn;
        day.OutputTokens += tokensOut;
        await db.SaveChangesAsync(ct);
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
        var (tool, response) = await AccurateToolCallAsync(InterestSystem, InterestTool, ask, ct);
        if (tool is null) return [];
        var parsed = JsonSerializer.Deserialize<InterestReport>(JsonSerializer.Serialize(tool.Input), Json) ?? new InterestReport();
        log.LogInformation("Interest written for {Count}/{Asked} items, {In}/{Out} tokens", parsed.Items.Count, lines.Count,
            response!.Usage.InputTokens, response.Usage.OutputTokens);
        return parsed.Items.Where(x => x.Index >= 0 && x.Index < lines.Count && !string.IsNullOrWhiteSpace(x.Interest))
            .GroupBy(x => x.Index).ToDictionary(g => g.Key, g => g.First().Interest!.Trim());
    }

    // The accurate model first. Opus 5.5 / Sonnet 5.5 do not allow a forced tool call, so the prompt asks for it.
    // A refusal, an error or no tool call -> the cheap model with a forced call, so a pile is never left without an answer.
    private Task<(ToolUseBlock? Tool, Message? Response)> AccurateToolCallAsync(string system, Tool tool, string ask, CancellationToken ct) =>
        AccurateToolCallAsync(system, tool, ask, InterestModel, ct);

    // The accurate model first (it does not allow a forced tool call, so the prompt asks for it); no tool call, a refusal
    // or an error -> the cheap model with a forced call, so nothing is ever left without an answer.
    private async Task<(ToolUseBlock? Tool, Message? Response)> AccurateToolCallAsync(string system, Tool tool, MessageParamContent ask, string accurateModel, CancellationToken ct)
    {
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        Message? response = null;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = accurateModel,
                MaxTokens = 16000,
                System = Cached(system),
                Tools = [tool],
                ToolChoice = new ToolChoiceAuto(),
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = [new() { Role = Role.User, Content = ask }],
            }, ct);
            await CountTokensAsync(response, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Accurate model {Model} failed - falling back to {Cheap}", accurateModel, Model);
        }

        var used = response?.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault();
        if (used is not null) return (used, response);
        if (response is not null) log.LogWarning("Accurate model gave no tool call (stop: {Stop}) - falling back", response.StopReason);
        response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 8000,
            System = Cached(system),
            Tools = [tool],
            ToolChoice = new ToolChoiceTool { Name = tool.Name },
            Messages = [new() { Role = Role.User, Content = ask }],
        }, ct);
        await CountTokensAsync(response, ct);
        return (response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault(), response);
    }

    private const string KolofonSystem = """
        Du læser kolofonsider (bagsiden af titelbladet) i brugte bøger til en dansk antikvarbutik.
        Hvert billede har et nummer. For HVERT billede: kald report_kolofon med det der står - præcis som trykt.
        - isbn: kun cifre (og evt. X), tomt hvis der ikke står et.
        - edition: udgave og oplag som det står, fx "1. udgave, 1. oplag" eller "2. udgave, 5. oplag"; "First edition"
          ved engelske bøger. Tomt hvis det ikke står.
        - year: årstallet for DETTE oplag (ikke copyright-året for originalen), null hvis det ikke kan ses.
        - publisher: forlaget.
        Gæt aldrig. Hvis billedet ikke er en kolofon eller ikke kan læses, så skriv det i note og lad resten være tomt.
        """;

    private static readonly Tool KolofonTool = new()
    {
        Name = "report_kolofon",
        Description = "Report what each numbered kolofon photo says",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["items"] = JsonDocument.Parse("""
                    {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "index":     { "type": "integer" },
                          "isbn":      { "type": "string" },
                          "edition":   { "type": "string" },
                          "year":      { "type": ["integer", "null"] },
                          "publisher": { "type": "string" },
                          "note":      { "type": "string" }
                        },
                        "required": ["index"]
                      }
                    }
                    """).RootElement,
            },
            Required = ["items"],
        },
    };

    // Up to 10 kolofon photos in ONE call (CLAUDE.md: never one call per photo), cheap model, same daily cap.
    public async Task<Dictionary<int, KolofonReading>> ReadKolofonsAsync(IReadOnlyList<byte[]> jpegs, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");
        if (jpegs.Count == 0) return [];
        await ReserveCallAsync(ct);
        var content = new List<ContentBlockParam>();
        for (var i = 0; i < jpegs.Count; i++)
        {
            content.Add(new TextBlockParam { Text = $"Billede {i}:" });
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(jpegs[i]), MediaType = "image/jpeg" } });
        }
        content.Add(new TextBlockParam { Text = $"Læs alle {jpegs.Count} kolofoner (billede 0 til {jpegs.Count - 1})." });
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 4000,
            System = Cached(KolofonSystem),
            Tools = [KolofonTool],
            ToolChoice = new ToolChoiceTool { Name = "report_kolofon" },
            Messages = [new() { Role = Role.User, Content = content }],
        }, ct);
        await CountTokensAsync(response, ct);
        var tool = response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault();
        var parsed = tool is null ? new KolofonReport() : JsonSerializer.Deserialize<KolofonReport>(JsonSerializer.Serialize(tool.Input), Json) ?? new KolofonReport();
        log.LogInformation("Kolofons read: {Count}/{Asked}, {In}/{Out} tokens", parsed.Items.Count, jpegs.Count,
            response.Usage.InputTokens, response.Usage.OutputTokens);
        return parsed.Items.Where(x => x.Index >= 0 && x.Index < jpegs.Count).GroupBy(x => x.Index).ToDictionary(g => g.Key, g => g.First().ToReading());
    }

    private const string IdentifySystem = """
        Du hjælper en dansk antikvarbutik med at finde ud af, præcis hvad en ting er (bog, tegneserie, blad, CD, DVD, plade, spil).
        Du får et billede og det, en person har skrevet om tingen. Personen har rettet det, AI'en først læste forkert - det, personen
        skrev, er udgangspunktet og kan være kun en del (fx kun serien). Find den RIGTIGE ting: læs titel, kunstner/forfatter,
        serie/blad, nummer og år af billedet, og brug din viden om danske udgivelser til at gøre navnet fuldt og rigtigt stavet.
        Er billedet en hel bunke, så find den ting, der passer til det skrevne, og læs kun den.
        Kald report_thing. Gæt aldrig: et felt, du ikke kan se eller ikke ved, er tomt (år = null). Skriv kort i note, hvad du
        er usikker på. kind er en af: Bog, Tegneserie, Superhelteblad, Anders And-blad, Jumbobog, Magasin, CD, DVD, Blu-ray, VHS,
        Vinyl, Computerspil, Brætspil, Rollespil, Andet.
        """;

    private static readonly Tool IdentifyTool = new()
    {
        Name = "report_thing",
        Description = "Report what the thing is",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["kind"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["title"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["artist"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["series"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["number"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
                ["year"] = JsonDocument.Parse("""{ "type": ["integer", "null"] }""").RootElement,
                ["note"] = JsonDocument.Parse("""{ "type": "string" }""").RootElement,
            },
        },
    };

    // ItemCheckUseCases: one call per press of Gem / new photo - a person starts it, never a loop. Cheap model, same daily cap.
    public async Task<Identified?> IdentifyAsync(byte[] jpeg, bool wholePile, string typed, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("AI er ikke slået til (Claude:ApiKey mangler).");
        await ReserveCallAsync(ct);
        var content = new List<ContentBlockParam>
        {
            new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(jpeg), MediaType = "image/jpeg" } },
            new TextBlockParam { Text = (wholePile ? "Billedet er en HEL BUNKE - find den ting, der passer.\n" : "Billedet er tingen selv.\n")
                + "Det har personen skrevet:\n" +(typed.Trim() == "" ? "(intet)" : typed) },
        };
        var client = new AnthropicClient { ApiKey = cfg["Claude:ApiKey"] };
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 800,
            System = Cached(IdentifySystem),
            Tools = [IdentifyTool],
            ToolChoice = new ToolChoiceTool { Name = "report_thing" },
            Messages = [new() { Role = Role.User, Content = content }],
        }, ct);
        await CountTokensAsync(response, ct);
        if (response.Content.Select(b => b.Value).OfType<ToolUseBlock>().FirstOrDefault() is not { } tool) return null;
        var r = JsonSerializer.Deserialize<RawItem>(JsonSerializer.Serialize(tool.Input), Json) ?? new RawItem();
        log.LogInformation("Thing identified: {Title} / {Series} {Number}, {In}/{Out} tokens", r.Title, r.Series, r.Number,
            response.Usage.InputTokens, response.Usage.OutputTokens);
        var found = new Identified((r.Kind ?? "").Trim() == "" ? "" : Kinds.Normalise(r.Kind), (r.Title ?? "").Trim(), (r.Artist ?? "").Trim(),
            (r.Series ?? "").Trim(), (r.Number ?? "").Trim(), r.Year is > 1800 and < 2100 ? r.Year : null, (r.Note ?? "").Trim());
        return found.Title == "" && found.Series == "" ? null : found;
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
        (r.Note ?? "").Trim(), (r.Interest ?? "").Trim(), EditionHint: EditionHints.Normalise(r.EditionHint), Usage: Usages.Normalise(r.Usage),
        Publisher: (r.Publisher ?? "").Trim(), Area: PhotoArea.From(r.Area), Language: Languages.Normalise(r.Language),
        Barcode: r.Kolofon == true ? (r.Isbn ?? "").Trim() : "", Kolofon: r.Kolofon == true, Edition: r.Kolofon == true ? (r.Edition ?? "").Trim() : "",
        StickerKr: r.StickerKr is > 0 and < 100000 ? r.StickerKr : null,
        StickerArea: r.StickerKr is > 0 and < 100000 ? PhotoArea.From(r.StickerArea, 0.004) : null);

    internal sealed class Report { public List<RawItem> Items { get; set; } = []; public string? Retake { get; set; } }
    internal sealed class KolofonReport { public List<KolofonRow> Items { get; set; } = []; }
    internal sealed class KolofonRow
    {
        public KolofonReading ToReading() => new(Isbn, Edition, Year, Publisher, Note);
        public int Index { get; set; }
        public string? Isbn { get; set; }
        public string? Edition { get; set; }
        public int? Year { get; set; }
        public string? Publisher { get; set; }
        public string? Note { get; set; }
    }
    internal sealed class InterestReport { public List<InterestRow> Items { get; set; } = []; }
    internal sealed class InterestRow { public int Index { get; set; } public string? Interest { get; set; } }

    internal sealed class RawItem
    {
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Series { get; set; }
        public string? Publisher { get; set; }
        public string? Number { get; set; }
        public int? Year { get; set; }
        public string? Platform { get; set; }
        public string? Condition { get; set; }
        public string? Usage { get; set; }
        public string? Language { get; set; }
        public int? Quantity { get; set; }
        public double? Confidence { get; set; }
        public string? Note { get; set; }
        public string? Interest { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("edition_hint")] public string? EditionHint { get; set; }
        public List<double>? Area { get; set; }
        public bool? Kolofon { get; set; }
        public string? Edition { get; set; }
        public string? Isbn { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("sticker_kr")] public int? StickerKr { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("sticker_area")] public List<double>? StickerArea { get; set; }
    }
}
