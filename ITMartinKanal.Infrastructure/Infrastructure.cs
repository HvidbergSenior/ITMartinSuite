using System.Text.Json;
using ITMartinKanal.Application;
using ITMartinKanal.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinKanal.Infrastructure;

// All comments in one JSON file on the data volume (DataDir/kanal.json) - a demo, small on purpose.
public sealed class JsonKanalStore : IKanalStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private readonly object _lock = new();
    private readonly KanalData _data;

    public JsonKanalStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "kanal.json");
        _data = File.Exists(_path) ? JsonSerializer.Deserialize<KanalData>(File.ReadAllText(_path), Json) ?? new KanalData() : new KanalData();
    }

    public KanalData Get()
    {
        lock (_lock) return _data;
    }

    public void Update(Action<KanalData> change)
    {
        lock (_lock)
        {
            change(_data);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Json));
            File.Move(tmp, _path, overwrite: true);
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddKanalInfrastructure(this IServiceCollection services, string dataDir) =>
        services.AddSingleton<IKanalStore>(new JsonKanalStore(dataDir));
}
