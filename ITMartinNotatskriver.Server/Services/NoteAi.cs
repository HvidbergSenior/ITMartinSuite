using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using DocumentFormat.OpenXml.Packaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ITMartinNotatskriver.Server.Services;

/// <summary>One thing the user added: a photo (already shrunk), a PDF, or text read from a file.</summary>
public sealed record Bilag(string Name, string Kind, byte[] Data, string Text)
{
    public string Thumb => Kind == "billede" ? "data:image/jpeg;base64," + Convert.ToBase64String(Data) : "";
}

public sealed record DokType(string Key, string Icon, string Title, string Instruction);

/// <summary>Turns notes, photos of handwriting and files into one finished document - ONE Claude call per
/// document, however many pages are attached. Nothing is stored; everything lives in the visitor's circuit.</summary>
public sealed class NoteAi(IConfiguration config, AiBudget budget, ILogger<NoteAi> logger)
{
    public const int MaxBilag = 10;

    public static readonly DokType[] Typer =
    [
        new("referat", "🗓", "Mødereferat", "Et mødereferat: overskrift med møde og dato hvis kendt, deltagere hvis nævnt, derefter punkterne drøftet, beslutninger og en liste 'Opgaver' med hvem/hvad/hvornår, når noterne siger det."),
        new("brev", "✉", "Brev", "Et brev med hilsen, tydelig besked i korte afsnit og afslutning. Afsender/modtager kun hvis noterne nævner dem."),
        new("mail", "📧", "Mail", "En kort mail: forslag til emnelinje først ('Emne: ...'), derefter mailen. Venlig og til sagen."),
        new("rapport", "📊", "Rapport", "En rapport med overskrifter: Baggrund, Hvad vi fandt, Konklusion, Næste skridt - kun de afsnit noterne giver indhold til."),
        new("ansoegning", "📝", "Ansøgning", "En ansøgning: hvad der søges, hvorfor, hvad ansøgeren kan/har gjort. Personlig men saglig."),
        new("opslag", "📣", "Opslag", "Et opslag til Facebook/LinkedIn: fanger i første linje, kort, letlæseligt, højst 3 emojis, evt. 2-3 hashtags til sidst."),
        new("telefon", "📞", "Telefonnotat", "Et telefonnotat: hvem, hvornår (hvis nævnt), hvad blev sagt, hvad blev aftalt."),
        new("plan", "✅", "Plan / huskeliste", "En overskuelig plan: grupperede punkter i rækkefølge, med datoer/ansvarlige hvis noterne nævner dem."),
        new("resume", "📄", "Resumé", "Et kort resumé på 3-6 sætninger med det vigtigste først."),
        new("renskriv", "🧾", "Renskriv mine noter", "Noterne renskrevet: samme indhold og rækkefølge, men hele sætninger, rettet stavning og ryddelig opsætning."),
    ];

    private const string System = """
        Du er Notatskriver. Brugeren giver dig sine egne stikord, noter, diktat eller billeder af håndskrevne noter
        og vil have et færdigt dokument på dansk.

        - Læs billeder af håndskrift omhyggeligt. Kan et ord ikke læses, så skriv dit bedste bud efterfulgt af [?].
        - Brug kun indholdet fra noterne. Opfind ikke navne, datoer, tal, aftaler eller fakta. Hvis dokumenttypen normalt
          har en oplysning, der mangler (fx dato eller modtager), så skriv en pladsholder i firkantede parenteser, fx [dato].
        - Bevar navne, tal, beløb og datoer nøjagtigt.
        - Skriv klart og naturligt dansk i hele sætninger. Ingen fyldord.
        - Formatér som almindelig tekst, der kan kopieres direkte ind i Word eller en mail: overskrifter på egen linje,
          punkter med "- ". Ingen markdown-stjerner eller #.
        - Svar kun med selve dokumentet - ingen indledning eller kommentar til brugeren.

        Spærre: Notatskriver må ikke bruges til patient- eller borgeroplysninger. Hvis materialet (også billederne)
        indeholder CPR-numre eller helbredsoplysninger om en bestemt person (fx notater fra en samtale med en patient
        eller borger), så skriv INTET dokument og svar kun med ordet BLOKERET.
        """;

    /// <summary>Claude's answer when the material turned out to hold patient/CPR data (see the system prompt).</summary>
    public const string Blocked = "BLOKERET";

    private readonly AnthropicClient _client = new() { ApiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Missing Claude:ApiKey") };

    public async Task<string> SkrivAsync(DokType type, string noter, IReadOnlyList<Bilag> bilag, string ekstra, CancellationToken ct)
    {
        budget.Take(type.Key);
        var content = new List<ContentBlockParam>();
        foreach (var b in bilag)
        {
            if (b.Kind == "billede")
                content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(b.Data), MediaType = MediaType.ImageJpeg } });
            else if (b.Kind == "pdf")
                content.Add(new DocumentBlockParam { Source = new Base64PdfSource { Data = Convert.ToBase64String(b.Data) } });
        }
        var sb = new StringBuilder()
            .AppendLine($"Lav: {type.Title}. {type.Instruction}");
        if (!string.IsNullOrWhiteSpace(ekstra)) sb.AppendLine($"Ekstra ønske fra brugeren: {ekstra.Trim()}");
        if (bilag.Any(b => b.Kind is "billede" or "pdf")) sb.AppendLine("Billederne/PDF'erne ovenfor er brugerens noter, i rækkefølge.");
        foreach (var b in bilag.Where(b => b.Kind == "tekst"))
            sb.AppendLine($"<fil navn=\"{b.Name}\">\n{b.Text}\n</fil>");
        if (!string.IsNullOrWhiteSpace(noter)) sb.AppendLine($"<noter>\n{noter.Trim()}\n</noter>");
        content.Add(new TextBlockParam { Text = sb.ToString() });

        try
        {
            var res = await _client.Messages.Create(new MessageCreateParams
            {
                Model = "claude-opus-5-5",
                MaxTokens = 16000,
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
                System = System,
                Messages = [new() { Role = Role.User, Content = content }],
            }, ct);
            logger.LogInformation("Notat {Type}: {In} in / {Out} out tokens, {Bilag} bilag", type.Key, res.Usage.InputTokens, res.Usage.OutputTokens, bilag.Count);
            if (res.StopReason == "refusal") throw new NoteException("AI'en ville ikke skrive dette dokument.");
            var text = string.Concat(res.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
            return res.StopReason == "max_tokens" ? text + "\n\n[Dokumentet blev for langt og er klippet af her]" : text;
        }
        catch (AnthropicRateLimitException) { throw new NoteException("Der er travlt lige nu. Vent et minut og prøv igen."); }
        catch (AnthropicApiException e)
        {
            logger.LogWarning(e, "Claude call failed");
            throw new NoteException("AI-tjenesten svarede ikke som den skulle. Prøv igen om lidt.");
        }
    }

    /// <summary>Reads one upload into a Bilag: photos are turned upright and shrunk (cheaper, and enough to read handwriting).</summary>
    public static async Task<Bilag> LaesAsync(string name, string contentType, Stream stream, CancellationToken ct)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (contentType.StartsWith("image/") || ext is ".jpg" or ".jpeg" or ".png" or ".heic" or ".webp" or ".gif")
        {
            try
            {
                using var img = Image.Load(bytes);
                img.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1800, 1800) }));
                img.Metadata.ExifProfile = null; // no GPS/camera data
                using var outMs = new MemoryStream();
                await img.SaveAsJpegAsync(outMs, new JpegEncoder { Quality = 85 }, ct);
                return new Bilag(name, "billede", outMs.ToArray(), "");
            }
            catch (Exception) { throw new NoteException($"Billedet {name} kunne ikke læses. På iPhone: vælg 'Mest kompatibel' under Indstillinger → Kamera → Formater."); }
        }
        if (ext == ".pdf" || contentType == "application/pdf") return new Bilag(name, "pdf", bytes, "");
        if (ext == ".docx")
        {
            using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
            var paras = doc.MainDocumentPart?.Document.Body?.Descendants<W.Paragraph>().Select(p => p.InnerText) ?? [];
            return new Bilag(name, "tekst", [], string.Join("\n", paras).Trim());
        }
        if (ext is ".txt" or ".md" or ".text" || contentType.StartsWith("text/"))
            return new Bilag(name, "tekst", [], Encoding.UTF8.GetString(bytes).Trim());
        throw new NoteException($"{name}: den filtype kan ikke læses. Brug billede, PDF, Word (.docx) eller tekst.");
    }

    /// <summary>The finished text as a simple Word document (one paragraph per line, headings in bold).</summary>
    public static byte[] Word(string text)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var body = new W.Body();
            var lines = text.Replace("\r", "").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // A short line followed by a blank line or text, without a full stop, reads as a heading.
                var heading = line.Length is > 0 and < 70 && !line.EndsWith('.') && !line.StartsWith("- ") && (i == 0 || lines[i - 1].Length == 0);
                var run = new W.Run(new W.Text(line) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
                if (heading) run.RunProperties = new W.RunProperties(new W.Bold());
                body.Append(new W.Paragraph(run));
            }
            doc.AddMainDocumentPart().Document = new W.Document(body);
        }
        return ms.ToArray();
    }
}

public sealed class NoteException(string message) : Exception(message);

/// <summary>Hard cap on Claude calls per day - the app is open to everyone, so the ceiling lives in code.</summary>
public sealed class AiBudget(IConfiguration config, ILogger<AiBudget> logger)
{
    private readonly int _maxPerDay = config.GetValue("Notatskriver:MaxAiCallsPerDay", 150);
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    public void Take(string what)
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= _maxPerDay)
            {
                logger.LogWarning("AI budget spent ({Used}/{Max}) - refused {What}", _used, _maxPerDay, what);
                throw new NoteException("Dagens gratis kvote er brugt op. Prøv igen i morgen – eller skriv til ITMartin@Mensa.dk for mere.");
            }
            _used++;
        }
    }
}
