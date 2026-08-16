using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITMartinStarRealms.Server.Services;

// Only called from the explicit "Generér med AI" button on Home when
// creating a brand-new ranked profile - never automatic (see CLAUDE.md AI
// cost discipline). One fal.ai Flux text-to-image call per click.
public sealed class ProfilePictureService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProfilePictureService> _logger;

    public ProfilePictureService(IHttpClientFactory factory, IWebHostEnvironment env, ILogger<ProfilePictureService> logger)
    {
        _http = factory.CreateClient("fal-profile-picture");
        _http.Timeout = TimeSpan.FromMinutes(2);
        _env = env;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("FalAi__ApiKey");
    }

    // Returns the app-relative URL (e.g. "/img/profiles/xxx.jpg") of the
    // saved image, or null if generation failed/unavailable.
    public async Task<string?> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                prompt = $"A fun, colorful sci-fi avatar icon: {prompt}. Simple, bold, centered, plain background, square composition.",
                image_size = "square",
                num_images = 1,
                enable_safety_checker = true
            });

            var req = new HttpRequestMessage(HttpMethod.Post, "https://fal.run/fal-ai/flux-pro/v1.1")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Key", _apiKey);

            var resp = await _http.SendAsync(req, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("fal.ai generation failed {Status}: {Body}", resp.StatusCode, raw);
                return null;
            }

            var doc = JsonDocument.Parse(raw).RootElement;
            var imageUrl = doc.GetProperty("images")[0].GetProperty("url").GetString();
            if (imageUrl is null) return null;

            var imageBytes = await _http.GetByteArrayAsync(imageUrl, ct);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var dir = Path.Combine(_env.WebRootPath, "img", "profiles");
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(Path.Combine(dir, fileName), imageBytes, ct);

            return $"/img/profiles/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile picture generation failed for prompt {Prompt}", prompt);
            return null;
        }
    }
}
