using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITMartinImageGen.Server.Services;

public sealed class FalAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public FalAiService(IConfiguration config, IHttpClientFactory factory)
    {
        _http   = factory.CreateClient("fal");
        _apiKey = (Environment.GetEnvironmentVariable("FalAi__ApiKey")
                   ?? config["FalAi:ApiKey"]
                   ?? throw new InvalidOperationException("FalAi__ApiKey not set")).Trim();
    }

    // Text → image using Flux
    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            prompt,
            image_size      = "square_hd",
            num_images      = 1,
            enable_safety_checker = false
        });

        var result = await PostAsync("https://fal.run/fal-ai/flux/dev", body, ct);
        return ExtractFirstImageUrl(result);
    }

    // Person in scene using IP-Adapter — photo passed as base64 data URL, no upload needed
    public async Task<string> PersonSwapAsync(string prompt, byte[] photoBytes, string mimeType, CancellationToken ct = default)
    {
        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(photoBytes)}";
        var body = JsonSerializer.Serialize(new
        {
            prompt,
            face_images_data_url = dataUrl,
            image_size           = "square_hd",
            num_images           = 1,
            scale                = 0.8
        });

        var result = await PostAsync("https://fal.run/fal-ai/ip-adapter-face-id", body, ct);
        return ExtractFirstImageUrl(result);
    }

    private async Task<JsonDocument> PostAsync(string url, string jsonBody, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", _apiKey);

        var resp = await _http.SendAsync(req, ct);
        var raw  = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"fal.ai {resp.StatusCode}: {raw}");

        return JsonDocument.Parse(raw);
    }

    private static string ExtractFirstImageUrl(JsonDocument doc)
    {
        var root = doc.RootElement;

        // Try images[0].url (Flux format)
        if (root.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
        {
            var first = images[0];
            if (first.TryGetProperty("url", out var u)) return u.GetString()!;
        }

        // Try image.url (some endpoints)
        if (root.TryGetProperty("image", out var image))
        {
            if (image.TryGetProperty("url", out var u)) return u.GetString()!;
            if (image.ValueKind == JsonValueKind.String) return image.GetString()!;
        }

        throw new InvalidOperationException($"Could not find image URL in fal.ai response: {doc.RootElement}");
    }
}
