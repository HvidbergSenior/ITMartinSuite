using System.Text.Json;

namespace ITMartinMitEl.Server.Services;

/// <summary>
/// Hourly kWh from one household's meter, kept so the history survives restarts and the
/// daily sync only has to fetch the last few days. One instance per household, handed out
/// by <see cref="HouseholdRegistry"/>.
/// </summary>
public sealed class ConsumptionStore
{
    private readonly object _lock = new();
    private readonly Action<Guid, string> _persist;
    // "yyyy-MM-dd HH" -> kWh per hour, and "q|yyyy-MM-dd HH:mm" -> kWh per quarter (15-minute meters).
    // One dictionary so the stored JSON keeps its shape; the "q|" keys never match an hour lookup.
    private Dictionary<string, double> _hours;
    private const string Q = "q|";

    public Guid HouseholdId { get; }
    public DateTime? LastSyncUtc { get; private set; }
    public string? LastSyncError { get; set; }

    public ConsumptionStore(Guid householdId, string? json, Action<Guid, string> persist)
    {
        HouseholdId = householdId;
        _persist = persist;
        _hours = Parse(json);
    }

    private static string Key(DateTime hourDk) => hourDk.ToString("yyyy-MM-dd HH");

    public void Merge(IEnumerable<EloverblikService.HourReading> readings)
    {
        lock (_lock)
        {
            foreach (var r in readings) _hours[Key(r.HourDk)] = r.Kwh;
            LastSyncUtc = DateTime.UtcNow;
            _persist(HouseholdId, JsonSerializer.Serialize(_hours));
        }
    }

    // Quarter readings replace the hours they fall in, so both views always agree.
    public void MergeQuarters(IReadOnlyCollection<EloverblikService.HourReading> quarters)
    {
        lock (_lock)
        {
            foreach (var r in quarters) _hours[Q + r.HourDk.ToString("yyyy-MM-dd HH:mm")] = r.Kwh;
            foreach (var h in EloverblikService.FoldToHours(quarters)) _hours[Key(h.HourDk)] = h.Kwh;
            LastSyncUtc = DateTime.UtcNow;
            _persist(HouseholdId, JsonSerializer.Serialize(_hours));
        }
    }

    // kWh per quarter-hour for one day, keyed by the quarter's start. Empty when only hours are known.
    public SortedDictionary<DateTime, double> Quarters(DateOnly date)
    {
        lock (_lock)
        {
            var prefix = Q + date.ToString("yyyy-MM-dd ");
            return new SortedDictionary<DateTime, double>(_hours.Where(kv => kv.Key.StartsWith(prefix))
                .ToDictionary(kv => DateTime.ParseExact(kv.Key[Q.Length..], "yyyy-MM-dd HH:mm", null), kv => kv.Value));
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

    // Every hour with a reading in [from, to) keyed by its Danish wall-clock hour.
    public Dictionary<DateTime, double> Range(DateOnly from, DateOnly to)
    {
        lock (_lock)
        {
            var lo = from.ToString("yyyy-MM-dd");
            var hi = to.ToString("yyyy-MM-dd");
            return _hours.Where(kv => string.CompareOrdinal(kv.Key, lo) >= 0 && string.CompareOrdinal(kv.Key, hi) < 0)
                .ToDictionary(kv => DateTime.ParseExact(kv.Key, "yyyy-MM-dd HH", null), kv => kv.Value);
        }
    }

    public bool HasAny { get { lock (_lock) return _hours.Count > 0; } }

    public DateOnly? LatestDay
    {
        get
        {
            lock (_lock)
            {
                var latest = _hours.Keys.Where(k => !k.StartsWith(Q)).DefaultIfEmpty().Max();
                return latest is null ? null : DateOnly.Parse(latest[..10]);
            }
        }
    }

    public string Serialize()
    {
        lock (_lock) return JsonSerializer.Serialize(_hours);
    }

    public static Dictionary<string, double> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, double>();
        try { return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new(); }
        catch (JsonException) { return new Dictionary<string, double>(); }
    }
}
