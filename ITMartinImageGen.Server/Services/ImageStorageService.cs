namespace ITMartinImageGen.Server.Services;

public sealed class ImageStorageService
{
    private readonly string _root;
    private readonly HttpClient _http;

    public ImageStorageService(IConfiguration config, IHttpClientFactory factory)
    {
        _root = config["ImageStorage:Root"] ?? "/app/data/images";
        _http = factory.CreateClient();
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveFromUrlAsync(string url, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var ext = DetectExtension(url, response.Content.Headers.ContentType?.MediaType);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var path = Path.Combine(_root, fileName);

        await using var fs = File.Create(path);
        await response.Content.CopyToAsync(fs, ct);
        return path;
    }

    public string[] GetSavedFiles()
    {
        if (!Directory.Exists(_root)) return [];
        return Directory.GetFiles(_root)
            .OrderByDescending(f => f)
            .ToArray();
    }

    private static string DetectExtension(string url, string? contentType)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains(".mp4")) return ".mp4";
        if (lower.Contains(".webm")) return ".webm";
        if (lower.Contains(".png")) return ".png";
        if (lower.Contains(".webp")) return ".webp";
        if (lower.Contains(".gif")) return ".gif";

        return contentType switch
        {
            "video/mp4"  => ".mp4",
            "image/png"  => ".png",
            "image/webp" => ".webp",
            "image/gif"  => ".gif",
            _            => ".jpg"
        };
    }
}
