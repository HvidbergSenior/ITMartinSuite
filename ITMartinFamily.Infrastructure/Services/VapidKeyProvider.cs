using System.Text.Json;
using WebPush;

namespace ITMartinFamily.Infrastructure.Services;

public sealed class VapidKeyProvider
{
    private readonly string _filePath;
    private (string PublicKey, string PrivateKey)? _cached;

    public VapidKeyProvider()
    {
        var dataDir = Directory.Exists("/app/data")
            ? "/app/data"
            : Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "vapid.json");
    }

    public string PublicKey => Load().PublicKey;

    public (string PublicKey, string PrivateKey) Load()
    {
        if (_cached.HasValue) return _cached.Value;

        if (File.Exists(_filePath))
        {
            var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(_filePath));
            if (stored?.PublicKey is { Length: > 0 } && stored.PrivateKey is { Length: > 0 })
            {
                _cached = (stored.PublicKey, stored.PrivateKey);
                return _cached.Value;
            }
        }

        var generated = VapidHelper.GenerateVapidKeys();
        File.WriteAllText(_filePath, JsonSerializer.Serialize(new StoredKeys(generated.PublicKey, generated.PrivateKey)));
        _cached = (generated.PublicKey, generated.PrivateKey);
        return _cached.Value;
    }

    private sealed record StoredKeys(string PublicKey, string PrivateKey);
}
