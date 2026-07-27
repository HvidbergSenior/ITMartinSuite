using Anthropic;
using Anthropic.Models.Messages;
using ITMartinDreamReader.Server.Data.Entities;

namespace ITMartinDreamReader.Server.Services;

// Premium-tier AI features for the dream journal. Kept deliberately cheap:
// one small Haiku call per dream entry (title + interpretation combined into
// a single call, not two), and the pattern-insight call only runs on-demand
// when the user taps the button on the graph page - never automatically on
// every entry or every page load.
public sealed class DreamAiService
{
    private readonly AnthropicClient? _client;
    private readonly string? _falApiKey;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _imagesRoot;

    public DreamAiService(IConfiguration config, IHttpClientFactory httpFactory)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };

        _falApiKey = config["FalAi:ApiKey"];
        _httpFactory = httpFactory;
        _imagesRoot = config["DreamImages:Root"] ?? "/app/data/images";
        Directory.CreateDirectory(_imagesRoot);
    }

    public bool IsAvailable => _client is not null;
    public bool IsImageAvailable => !string.IsNullOrWhiteSpace(_falApiKey);

    public async Task<(string title, string interpretation, string funny)> AnalyzeDreamAsync(
        List<string> categoryNames, string rating, string? note)
    {
        if (_client is null) return ("", "", "");

        var categoriesText = categoryNames.Count > 0 ? string.Join(", ", categoryNames) : "ingen valgt";
        var noteText = string.IsNullOrWhiteSpace(note) ? "" : $"\n\nNoter fra brugeren: {note}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                Du hjælper nogen med at forstå en drøm de lige har haft, ud fra hvilke temaer de har valgt
                og evt. egne noter. Svar ALTID på dansk. Giv TO fortolkninger: en rigtig og en for sjov.
                Formatér PRÆCIST sådan (ingen anden tekst før eller efter):
                TITEL: [3-6 ord, poetisk/fængende titel til drømmen]
                FORTOLKNING: [2-4 sætninger, varmt og nysgerrigt - ikke klinisk, ikke overdrevet
                mystisk/spirituelt. Det er en tolkning, ikke en facitliste]
                SJOVT: [1-2 korte, sjove/skæve sætninger om drømmen - kærligt drilsk, ikke ondskabsfuldt]
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Temaer i drømmen: {categoriesText}\nHumør ved opvågning: {rating}{noteText}"
                }
            ]
        });

        var text = "";
        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) { text = tb.Text.Trim(); break; }

        var title = "";
        var interpretation = "";
        var funny = "";
        var current = "";
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("TITEL:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["TITEL:".Length..].Trim();
                current = "";
            }
            else if (line.StartsWith("FORTOLKNING:", StringComparison.OrdinalIgnoreCase))
            {
                interpretation = line["FORTOLKNING:".Length..].Trim();
                current = "interpretation";
            }
            else if (line.StartsWith("SJOVT:", StringComparison.OrdinalIgnoreCase))
            {
                funny = line["SJOVT:".Length..].Trim();
                current = "funny";
            }
            else if (!string.IsNullOrWhiteSpace(line) && current == "interpretation")
            {
                interpretation += " " + line.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(line) && current == "funny")
            {
                funny += " " + line.Trim();
            }
        }

        return (title, interpretation, funny);
    }

    // Generates one image via fal.ai (Flux) depicting the dream and saves it
    // locally under /app/data/images (fal.ai's own URL is temporary - same
    // download-and-persist pattern as ITMartinImageGen.Server's
    // ImageStorageService). Returns a relative path served via
    // /dream-image?file=... Deliberately on-demand only, never automatic on
    // save - image generation costs real money per call, unlike the cheap
    // Haiku text calls above.
    public async Task<string?> GenerateDreamImageAsync(int entryId, List<string> categoryNames, string? note)
    {
        if (string.IsNullOrWhiteSpace(_falApiKey)) return null;

        var scenePrompt = categoryNames.Count > 0 ? string.Join(", ", categoryNames) : "a mysterious dream";
        var noteHint = string.IsNullOrWhiteSpace(note) ? "" : $". Dream details: {note}";
        var prompt = $"A dreamlike, surreal, painterly illustration of a dream involving: {scenePrompt}{noteHint}. " +
                     "Soft ethereal lighting, slightly abstract, evocative and atmospheric, not photorealistic.";

        var http = _httpFactory.CreateClient("fal");
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            prompt,
            image_size = "square_hd",
            num_images = 1,
            enable_safety_checker = true
        });

        var req = new HttpRequestMessage(HttpMethod.Post, "https://fal.run/fal-ai/flux-pro/v1.1")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Key", _falApiKey);

        var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        var raw = await resp.Content.ReadAsStringAsync();
        var doc = System.Text.Json.JsonDocument.Parse(raw);
        var root = doc.RootElement;

        string? imageUrl = null;
        if (root.TryGetProperty("images", out var images) && images.GetArrayLength() > 0
            && images[0].TryGetProperty("url", out var u))
            imageUrl = u.GetString();

        if (imageUrl is null) return null;

        var imgResp = await http.GetAsync(imageUrl);
        if (!imgResp.IsSuccessStatusCode) return null;

        var fileName = $"dream-{entryId}.jpg";
        var path = Path.Combine(_imagesRoot, fileName);
        await using (var fs = File.Create(path))
            await imgResp.Content.CopyToAsync(fs);

        return fileName;
    }

    public async Task<string> GetPatternInsightAsync(List<DreamEntry> recentEntries)
    {
        if (_client is null || recentEntries.Count == 0) return "";

        var summary = string.Join("\n", recentEntries.Select(e =>
        {
            var line = $"- {e.CreatedAt:d. MMM}: {string.Join(", ", e.Categories.Select(c => c.Name))} (humør: {e.Rating})";
            if (!string.IsNullOrWhiteSpace(e.Note)) line += $"\n  Noter: {e.Note}";
            return line;
        }));

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                Du analyserer mønstre i en persons seneste drømme - både temaerne/humøret OG det de selv
                har skrevet i deres noter (hvis der er noter). Læs noterne for tilbagevendende personer,
                steder, følelser eller situationer på tværs af drømmene, ikke kun kategori-optællinger.
                Svar på dansk, kort (3-6 sætninger), venligt og konkret - peg på faktiske gentagelser eller
                sammenhænge, ikke generiske observationer. Hvis der ikke er tydelige mønstre, sig det ærligt
                i stedet for at opfinde noget.
                """,
            Messages =
            [
                new() { Role = Role.User, Content = $"Seneste drømme:\n{summary}" }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();

        return "";
    }
}
