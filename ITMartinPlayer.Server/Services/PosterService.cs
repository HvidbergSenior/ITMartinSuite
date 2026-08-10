using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITMartinPlayer.Server.Services;

// A silly "concert poster" image for each performance - one image per
// performance (not per file, not per frame), using fal.ai's cheapest/fastest
// text-to-image model. Deliberately image-only, not video: video generation
// on fal.ai is far slower and far more expensive per call, and a poster
// generated in a few seconds fits the pace of a live karaoke night far
// better than a minutes-long video render would.
public sealed class PosterService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public bool IsAvailable => _apiKey is not null;

    public PosterService(IConfiguration config, IHttpClientFactory factory)
    {
        _http = factory.CreateClient("fal");
        _http.Timeout = TimeSpan.FromMinutes(2);
        _apiKey = Environment.GetEnvironmentVariable("FalAi__ApiKey") ?? config["FalAi:ApiKey"];
    }

    public async Task<string> GeneratePosterAsync(string title, string artist, string singerName, CancellationToken ct = default)
    {
        if (_apiKey is null) throw new InvalidOperationException("FalAi__ApiKey ikke konfigureret");

        var prompt =
            $"retro karaoke night concert poster, bold playful typography, the name \"{singerName}\" as the star performer, " +
            $"performing \"{title}\"{(string.IsNullOrWhiteSpace(artist) ? "" : $" by {artist}")}, " +
            "colorful neon stage lights, confetti, festive party atmosphere, illustrated poster art, no photorealistic faces";

        var body = JsonSerializer.Serialize(new { prompt, image_size = "square_hd", num_images = 1 });

        var req = new HttpRequestMessage(HttpMethod.Post, "https://fal.run/fal-ai/flux/schnell")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", _apiKey);

        var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"fal.ai {resp.StatusCode}: {raw}");

        var doc = JsonDocument.Parse(raw).RootElement;
        if (doc.TryGetProperty("images", out var images) && images.GetArrayLength() > 0 &&
            images[0].TryGetProperty("url", out var u))
            return u.GetString()!;

        throw new InvalidOperationException($"Kunne ikke finde billede-URL i svar: {raw[..Math.Min(300, raw.Length)]}");
    }
}
