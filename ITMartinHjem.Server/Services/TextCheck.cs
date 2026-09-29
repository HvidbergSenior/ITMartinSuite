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

    public enum Kind { Liste, Linje, Opslag }

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
            Kind.Liste => "Teksten er en liste over opgaver. Skriv den som punktform: én kort opgave pr. linje, uden tegn foran (ingen *, - eller •), uden punktum til sidst. Del sætninger med flere opgaver op, så hver opgave er sit eget punkt (fx \"skrive på X og ringe til Y\" bliver to linjer: \"Skrive på X\" og \"Ringe til Y\"). Start hvert punkt med stort bogstav. Tilføj ingen nye opgaver.",
            Kind.Linje => "Teksten er én kort linje til et billede eller en statusopdatering. Hold den kort, på én linje.",
            _ => "Teksten er et opslag. Bevar afsnit og opbygning; brug punktform, hvor der er en opremsning.",
        };
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeSonnet4_6,   // Haiku missed Danish names and did not split tasks into bullets (tested 2026-09-29)
                MaxTokens = 1500,
                System = "Du er korrekturlæser for Martin, der lægger tekst på sin egen hjemmeside (itmartin.dk). " +
                         "Ret stavefejl, slåfejl og grammatik på dansk. Martins egne navne staves sådan: ITMartin, ITKolibri, MinElpris, ElPriser, Svar, Bogshoppen, Forløbet, Polstrer, R6, Mensa. " + form + " " +
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
