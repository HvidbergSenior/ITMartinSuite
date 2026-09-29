using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinHjem.Server.Services;

// Checks Martin's own text before it goes on the site (Svar): spelling, bullet form for task lists,
// and anything a visitor could misread. One Sonnet call per save the owner presses - never in a loop -
// with a hard daily cap in code. Returns null when AI is not set up or fails: the text is then saved as written.
public sealed class TextCheck(IConfiguration config, ILogger<TextCheck> logger)
{
    public sealed record Result(string Suggested, List<string> Notes, bool Changed);

    public enum Kind { Liste, Linje, Opslag, Chat }

    private const int MaxPerDay = 200;
    private readonly AnthropicClient? _client = string.IsNullOrWhiteSpace(config["Claude:ApiKey"]) ? null : new AnthropicClient { ApiKey = config["Claude:ApiKey"] };
    private readonly object _lock = new();
    private DateOnly _day;
    private int _used;

    public async Task<Result?> CheckAsync(string text, Kind kind, CancellationToken ct = default)
    {
        text = text.Trim();
        if (_client is null || text.Length == 0 || !Take()) return null;
        var form = kind switch
        {
            Kind.Liste => "Hele teksten er en opgaveliste, så ALT skal i punktform.",
            Kind.Linje => "Teksten er en kort tekst (en linje til et billede, en status, en overskrift eller en kort beskrivelse). Er der ingen opgaver, så hold den kort som almindelig tekst.",
            Kind.Chat => "Teksten er Martins svar til en besøgende i chatten på hjemmesiden. Hold tonen venlig og personlig, som Martin skrev den.",
            _ => "Teksten er et opslag eller en side på hjemmesiden. Bevar afsnit og opbygning, og lad markdown, billedlinjer, links og emojis stå uændret; brug også punktform, hvor der er en opremsning.",
        };
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeSonnet4_6,   // Haiku missed Danish names and did not split tasks into bullets (tested 2026-09-29)
                MaxTokens = kind == Kind.Opslag ? 6000 : 1500,   // whole pages come back in full
                System = "Du er korrekturlæser for Martin, der lægger tekst på sin egen hjemmeside (itmartin.dk). " +
                         "Ret stavefejl, slåfejl og grammatik på dansk. Martins egne navne staves sådan: ITMartin, ITKolibri, MinElpris, ElPriser, Svar, Bogshoppen, Forløbet, Polstrer, R6, Mensa. " +
                         "OPGAVER SKAL ALTID STÅ I PUNKTFORM: når teksten nævner opgaver eller gøremål (noget Martin skal, vil, regner med eller har lavet), " +
                         "skriv dem som punkter – én opgave pr. linje, hver linje starter med \"* \", stort begyndelsesbogstav, intet punktum til sidst. " +
                         "Del sætninger med flere opgaver op (fx \"skrive på X og ringe til Y\" bliver \"* Skrive på X\" og \"* Ringe til Y\"). " +
                         "Almindelig tekst omkring opgaverne står som almindelig tekst over eller under punkterne. Tilføj ingen nye opgaver. " + form + " " +
                         "Bevar Martins egne ord, tone og humor (også emojis) – omskriv ikke mere end nødvendigt, og opfind intet. " +
                         "Se efter ting, der kan misforstås af en fremmed læser: tvetydige formuleringer, ironi der kan læses bogstaveligt, " +
                         "noget der kan virke stødende, løfter om priser eller tider, og personlige oplysninger om andre (navne på børn, adresser, helbred). " +
                         "Skriv hver sådan ting som en kort note på dansk, der siger hvad og hvorfor. Stavefejl skal IKKE i noterne – de er rettet i teksten. Ingen noter, hvis der ikke er noget. " +
                         "Svar KUN med JSON: {\"tekst\": \"den rettede tekst\", \"noter\": [\"…\"]}. Linjeskift i teksten som \\n.",
                Messages = [new() { Role = Role.User, Content = text }]
            }, cancellationToken: ct);

            var raw = "";
            foreach (var block in response.Content)
                if (block.TryPickText(out var tb)) raw += tb.Text;
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var suggested = (doc.RootElement.TryGetProperty("tekst", out var t) ? t.GetString() : null)?.Trim() ?? text;
            var notes = doc.RootElement.TryGetProperty("noter", out var n) && n.ValueKind == JsonValueKind.Array
                ? n.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                : [];
            if (suggested.Length == 0) suggested = text;
            return new Result(suggested, notes, !Same(suggested, text));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Text check failed - saving as written");
            return null;
        }
    }

    // Whitespace-only differences are not a change worth asking about.
    private static bool Same(string a, string b) =>
        string.Join('\n', a.Split('\n').Select(l => l.Trim())) == string.Join('\n', b.Split('\n').Select(l => l.Trim()));

    private bool Take()
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= MaxPerDay) { logger.LogWarning("Text check budget spent ({Max}/day)", MaxPerDay); return false; }
            _used++;
            return true;
        }
    }
}
