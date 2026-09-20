using System.Text.Json;

namespace ITMartinLadestander.Server.Services;

// Single JSON file, same pattern as ElPriser's HouseholdStore.
public sealed class ForeningStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly ForeningData _data;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public event Action? Changed;

    public ForeningStore(IConfiguration cfg)
    {
        var dir = cfg["DataDir"] ?? "/data";
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "forening.json");
        _data = File.Exists(_path)
            ? JsonSerializer.Deserialize<ForeningData>(File.ReadAllText(_path)) ?? new()
            : new();
        if (!File.Exists(_path)) Save();
    }

    public ForeningData Get() { lock (_gate) return _data; }

    public void Update(Action<ForeningData> mutate)
    {
        lock (_gate) { mutate(_data); Save(); }
        Changed?.Invoke();
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Json));
        File.Move(tmp, _path, overwrite: true);
    }
}
