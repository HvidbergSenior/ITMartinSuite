using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinStarRealms.Server.Services;

// Only called from the explicit "Generér med AI" button on Home when
// creating a brand-new ranked profile - never automatic (see CLAUDE.md AI
// cost discipline). One fal.ai Flux text-to-image call per click, plus one
// cheap Haiku translation call (see TranslatePromptAsync below).
public sealed class ProfilePictureService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly AnthropicClient? _claude;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProfilePictureService> _logger;

    public ProfilePictureService(IHttpClientFactory factory, IConfiguration configuration, IWebHostEnvironment env, ILogger<ProfilePictureService> logger)
    {
        _http = factory.CreateClient("fal-profile-picture");
        _http.Timeout = TimeSpan.FromMinutes(2);
        _env = env;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("FalAi__ApiKey");

        var claudeKey = Environment.GetEnvironmentVariable("Claude__ApiKey") ?? configuration["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(claudeKey))
            _claude = new AnthropicClient { ApiKey = claudeKey };
    }

    // fal.ai's Flux model is trained overwhelmingly on English prompts - a
    // Danish prompt fed straight through (e.g. "en rumpirat med solbriller")
    // produces poor or unrelated results. Translate to a short English visual
    // description first. Best-effort: if Claude isn't configured or the call
    // fails, fall back to the raw prompt rather than blocking generation.
    private async Task<string> TranslatePromptAsync(string danishPrompt, CancellationToken ct)
    {
        if (_claude is null) return danishPrompt;

        try
        {
            var request = new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 100,
                System = """
                    Translate the user's short Danish image description into a
                    concise English visual description suitable for an
                    AI image generator. Return ONLY the translated
                    description, nothing else - no quotes, no preamble.
                    """,
                Messages = [new() { Role = Role.User, Content = danishPrompt }]
            };

            var response = await _claude.Messages.Create(request, cancellationToken: ct);
            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var tb) && !string.IsNullOrWhiteSpace(tb.Text))
                    return tb.Text.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prompt translation failed, using raw prompt");
        }

        return danishPrompt;
    }

    // Returns the app-relative URL (e.g. "/img/profiles/xxx.jpg") of the
    // saved image, or null if generation failed/unavailable.
    public async Task<string?> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;

        try
        {
            var englishPrompt = await TranslatePromptAsync(prompt, ct);

            var body = JsonSerializer.Serialize(new
            {
                prompt = $"A fun, colorful sci-fi avatar icon: {englishPrompt}. Simple, bold, centered, plain background, square composition.",
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
