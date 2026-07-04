using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITMartinImageGen.Server.Services;

public sealed class FalAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public FalAiService(IConfiguration config, IHttpClientFactory factory)
    {
        _http         = factory.CreateClient("fal");
        _http.Timeout = TimeSpan.FromMinutes(15);
        _apiKey       = (Environment.GetEnvironmentVariable("FalAi__ApiKey")
                         ?? config["FalAi:ApiKey"]
                         ?? throw new InvalidOperationException("FalAi__ApiKey not set")).Trim();
    }

    // Text → image (Flux Dev)
    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            prompt,
            image_size            = "square_hd",
            num_images            = 1,
            enable_safety_checker = false
        });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/flux-pro/v1.1", body, ct));
    }

    // Image → image: transform with text prompt and strength (Flux Dev image-to-image)
    public async Task<string> ImageToImageAsync(string imageUrl, string prompt, float strength = 0.75f, CancellationToken ct = default)
    {
        var uploadedUrl = await EnsureUploadedAsync(imageUrl, ct);
        var body = JsonSerializer.Serialize(new
        {
            image_url             = uploadedUrl,
            prompt,
            strength,
            image_size            = "square_hd",
            num_images            = 1,
            enable_safety_checker = false
        });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/flux-dev/image-to-image", body, ct));
    }

    // Face swap: replace the face in sceneUrl with the face from faceBytes
    public async Task<string> FaceSwapAsync(string sceneUrl, byte[] faceBytes, string faceMimeType, CancellationToken ct = default)
    {
        var sceneUploadUrl = await EnsureUploadedAsync(sceneUrl, ct);
        var faceExt        = faceMimeType.Contains("png") ? "png" : "jpg";
        var faceUploadUrl  = await UploadAsync(faceBytes, faceMimeType, $"face.{faceExt}", ct);

        var body = JsonSerializer.Serialize(new
        {
            base_image_url = sceneUploadUrl,
            swap_image_url = faceUploadUrl,
        });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/face-swap", body, ct));
    }

    // Background removal → transparent PNG
    public async Task<string> RemoveBackgroundAsync(string imageUrl, CancellationToken ct = default)
    {
        var uploadedUrl = await EnsureUploadedAsync(imageUrl, ct);
        var body = JsonSerializer.Serialize(new { image_url = uploadedUrl });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/imageutils/rembg", body, ct));
    }

    // Upscale 4× (ESRGAN)
    public async Task<string> UpscaleAsync(string imageUrl, CancellationToken ct = default)
    {
        var uploadedUrl = await EnsureUploadedAsync(imageUrl, ct);
        var body = JsonSerializer.Serialize(new
        {
            image_url    = uploadedUrl,
            scale        = 4,
            face_enhance = true
        });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/esrgan", body, ct));
    }

    // Virtual clothes try-on (CatVTON)
    public async Task<string> VirtualTryOnAsync(string personUrl, byte[] garmentBytes, string garmentMime,
        string category = "upper", CancellationToken ct = default)
    {
        var personUploadUrl  = await EnsureUploadedAsync(personUrl, ct);
        var garmentExt       = garmentMime.Contains("png") ? "png" : "jpg";
        var garmentUploadUrl = await UploadAsync(garmentBytes, garmentMime, $"garment.{garmentExt}", ct);

        var body = JsonSerializer.Serialize(new
        {
            human_image_url   = personUploadUrl,
            garment_image_url = garmentUploadUrl,
            cloth_type        = category
        });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/cat-vton", body, ct));
    }

    // Precise local editing with Flux Pro Kontext
    public async Task<string> KontextEditAsync(string imageUrl, string prompt, CancellationToken ct = default)
    {
        var uploadedUrl = await EnsureUploadedAsync(imageUrl, ct);
        var body = JsonSerializer.Serialize(new { image_url = uploadedUrl, prompt, num_images = 1 });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/flux-pro/kontext", body, ct));
    }

    // Style transfer with TeleStyle v2
    public async Task<string> StyleTransferAsync(string contentUrl, byte[] styleBytes, string styleMime, CancellationToken ct = default)
    {
        var contentUploadUrl = await EnsureUploadedAsync(contentUrl, ct);
        var ext = styleMime.Contains("png") ? "png" : "jpg";
        var styleUploadUrl  = await UploadAsync(styleBytes, styleMime, $"style.{ext}", ct);
        var body = JsonSerializer.Serialize(new { content_image_url = contentUploadUrl, style_image_url = styleUploadUrl });
        return ExtractFirstImageUrl(await PostAsync("https://fal.run/fal-ai/telestyle-v2", body, ct));
    }

    // Upload a file to fal.ai storage and return its permanent CDN URL.
    // Two-step: initiate (get presigned upload_url + file_url) → PUT file → return file_url.
    private async Task<string> UploadAsync(byte[] data, string mimeType, string filename, CancellationToken ct)
    {
        // Step 1: initiate — get presigned upload URL
        var initBody = JsonSerializer.Serialize(new { file_name = filename, content_type = mimeType });
        var initReq  = new HttpRequestMessage(HttpMethod.Post,
            "https://rest.fal.ai/storage/upload/initiate?storage_type=fal-cdn-v3")
        {
            Content = new StringContent(initBody, System.Text.Encoding.UTF8, "application/json")
        };
        initReq.Headers.Authorization = new AuthenticationHeaderValue("Key", _apiKey);

        var initResp = await _http.SendAsync(initReq, ct);
        var initRaw  = await initResp.Content.ReadAsStringAsync(ct);
        if (!initResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"fal.ai upload initiate {initResp.StatusCode}: {initRaw}");

        var initDoc   = JsonDocument.Parse(initRaw).RootElement;
        var uploadUrl = initDoc.GetProperty("upload_url").GetString()!;
        var fileUrl   = initDoc.GetProperty("file_url").GetString()!;

        // Step 2: PUT the file to the presigned URL (no auth header needed)
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

        var putResp = await _http.PutAsync(uploadUrl, fileContent, ct);
        if (!putResp.IsSuccessStatusCode)
        {
            var putRaw = await putResp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"fal.ai upload PUT {putResp.StatusCode}: {putRaw}");
        }

        return fileUrl;
    }

    // Convert data URL to fal.ai storage URL if needed; pass through regular URLs unchanged
    private async Task<string> EnsureUploadedAsync(string url, CancellationToken ct)
    {
        if (!url.StartsWith("data:")) return url;
        var comma = url.IndexOf(',');
        var mime  = url[5..comma].Split(';')[0];
        var bytes = Convert.FromBase64String(url[(comma + 1)..]);
        var ext   = mime.Contains("png") ? "png" : "jpg";
        return await UploadAsync(bytes, mime, $"image.{ext}", ct);
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

        if (root.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
        {
            var first = images[0];
            if (first.TryGetProperty("url", out var u)) return u.GetString()!;
        }
        if (root.TryGetProperty("image", out var image))
        {
            if (image.TryGetProperty("url", out var u)) return u.GetString()!;
            if (image.ValueKind == JsonValueKind.String) return image.GetString()!;
        }
        if (root.TryGetProperty("url", out var rootUrl)) return rootUrl.GetString()!;

        throw new InvalidOperationException($"Could not find image URL in fal.ai response: {doc.RootElement}");
    }
}
