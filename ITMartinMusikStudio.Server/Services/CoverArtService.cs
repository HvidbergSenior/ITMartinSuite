using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ITMartinMusikStudio.Server.Data.Entities;

namespace ITMartinMusikStudio.Server.Services;

// Trimmed port of ITMartinImageGen.Server's FalAiService (text-to-image only,
// same fal.ai Flux Pro endpoint) + ImageStorageService's download-and-save
// pattern, specialized for song covers: saved as {songKey}.{ext} under
// Root/covers with overwrite semantics (one current cover, not a version
// history like takes/sketches). Deliberately skips ImageGen's extra
// ClaudePromptService "prompt refinement" call - this builds a direct prompt
// from the song's own fields, keeping generation to exactly one paid API
// call per click (per-event, cheapest path - see the Package3 cost-ceiling
// rule: AI features must never run away in cost).
public sealed class CoverArtService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly StudioLibraryService _library;

    public CoverArtService(IConfiguration config, IHttpClientFactory factory, StudioLibraryService library)
    {
        _http = factory.CreateClient("fal");
        _http.Timeout = TimeSpan.FromMinutes(5);
        _apiKey = Environment.GetEnvironmentVariable("FalAi__ApiKey") ?? config["FalAi:ApiKey"];
        _library = library;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    public static string BuildPrompt(StudioSong song)
    {
        var subject = string.IsNullOrWhiteSpace(song.Artist)
            ? $"an original song called \"{song.Title}\""
            : $"the song \"{song.Title}\" by {song.Artist}";
        return $"Album cover art for {subject}. Evocative, mood-driven illustration matching the song's " +
               "feeling, no text, no words, no letters, no lyrics, square composition, professional album " +
               "artwork style, high detail.";
    }

    public async Task<string> GenerateAsync(StudioSong song, CancellationToken ct = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("FalAi:ApiKey not configured");

        var body = JsonSerializer.Serialize(new
        {
            prompt = BuildPrompt(song),
            image_size = "square_hd",
            num_images = 1,
            enable_safety_checker = false
        });

        var req = new HttpRequestMessage(HttpMethod.Post, "https://fal.run/fal-ai/flux-pro/v1.1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", _apiKey);

        var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"fal.ai {resp.StatusCode}: {raw}");

        var doc = JsonDocument.Parse(raw);
        var imageUrl = doc.RootElement.GetProperty("images")[0].GetProperty("url").GetString()
            ?? throw new InvalidOperationException("No image URL in fal.ai response");

        var imgResp = await _http.GetAsync(imageUrl, ct);
        imgResp.EnsureSuccessStatusCode();
        var ext = imageUrl.Contains(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

        var dir = Path.Combine(_library.Root, "covers");
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, $"{song.Key}{ext}");
        await using (var fs = File.Create(dest))
            await imgResp.Content.CopyToAsync(fs, ct);

        return Path.GetRelativePath(_library.Root, dest).Replace('\\', '/');
    }
}
