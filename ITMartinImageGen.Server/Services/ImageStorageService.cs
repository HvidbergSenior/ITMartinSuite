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

    public async Task<string> SaveFromUrlAsync(string imageUrl, CancellationToken ct = default)
    {
        var bytes    = await _http.GetByteArrayAsync(imageUrl, ct);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.jpg";
        var path     = Path.Combine(_root, fileName);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }

    public string[] GetSavedImages()
    {
        if (!Directory.Exists(_root)) return [];
        return Directory.GetFiles(_root, "*.jpg")
            .OrderByDescending(f => f)
            .ToArray();
    }
}
