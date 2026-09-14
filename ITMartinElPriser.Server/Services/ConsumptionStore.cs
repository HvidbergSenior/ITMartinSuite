using System.Text.Json;

namespace ITMartinElPriser.Server.Services;

// Hourly kWh from the meter, kept on disk so the history survives restarts
// and the daily sync only has to fetch the last few days.
public sealed class ConsumptionStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, double> _hours; // "yyyy-MM-dd HH" -> kWh

    public DateTime? LastSyncUtc { get; private set; }
    public string? LastSyncError { get; set; }

    public ConsumptionStore(IConfiguration config)
    {
        var dataDir = config["DataDir"] ?? "/data";
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "consumption.json");
        _hours = Load();
    }

    private static string Key(DateTime hourDk) => hourDk.ToString("yyyy-MM-dd HH");

    public void Merge(IEnumerable<EloverblikService.HourReading> readings)
    {
        lock (_lock)
        {
            foreach (var r in readings) _hours[Key(r.HourDk)] = r.Kwh;
            LastSyncUtc = DateTime.UtcNow;
            File.WriteAllText(_path, JsonSerializer.Serialize(_hours));
        }
    }

    public Dictionary<int, double> Day(DateOnly date)
    {
        lock (_lock)
        {
            var prefix = date.ToString("yyyy-MM-dd ");
            return _hours.Where(kv => kv.Key.StartsWith(prefix))
                .ToDictionary(kv => int.Parse(kv.Key[^2..]), kv => kv.Value);
        }
    }

    public bool HasAny { get { lock (_lock) return _hours.Count > 0; } }

    public DateOnly? LatestDay
    {
        get
        {
            lock (_lock)
            {
                if (_hours.Count == 0) return null;
                return DateOnly.Parse(_hours.Keys.Max()![..10]);
            }
        }
    }

    private Dictionary<string, double> Load()
    {
        if (!File.Exists(_path)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(_path)) ?? new(); }
        catch { return new(); }
    }
}
