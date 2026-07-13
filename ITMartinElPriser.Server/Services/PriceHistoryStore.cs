using System.Text.Json;

namespace ITMartinElPriser.Server.Services;

public sealed class WeeklyPatternPoint
{
    public DayOfWeek Day { get; set; }
    public int Hour { get; set; }
    public double AvgPriceKrPerKwh { get; set; }
    public int SampleCount { get; set; }
}

// Keeps a rolling ~9 days of actually-observed prices on disk so we can show a
// "typical week" pattern. Real day-ahead spot prices only ever exist ~1 day out,
// so this is built from history, not forecast - it grows more useful over time
// instead of pretending to know next Thursday's price today.
public sealed class PriceHistoryStore
{
    private readonly string _dataDir;
    private readonly object _lock = new();

    public PriceHistoryStore(IConfiguration config)
    {
        _dataDir = config["DataDir"] ?? "/data";
        Directory.CreateDirectory(_dataDir);
    }

    private string PathFor(string priceArea) => Path.Combine(_dataDir, $"price-history-{priceArea}.json");

    public void Merge(string priceArea, List<PricePoint> newPoints)
    {
        lock (_lock)
        {
            var existing = LoadRaw(priceArea);
            var byHour = existing.ToDictionary(p => p.TimeUtc);

            foreach (var p in newPoints)
                byHour[p.TimeUtc] = p;

            var cutoff = DateTime.UtcNow.AddDays(-9);
            var merged = byHour.Values
                .Where(p => p.TimeUtc >= cutoff)
                .OrderBy(p => p.TimeUtc)
                .ToList();

            File.WriteAllText(PathFor(priceArea), JsonSerializer.Serialize(merged));
        }
    }

    private List<PricePoint> LoadRaw(string priceArea)
    {
        var path = PathFor(priceArea);
        if (!File.Exists(path)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<PricePoint>>(File.ReadAllText(path)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public List<WeeklyPatternPoint> GetWeeklyPattern(string priceArea)
    {
        lock (_lock)
        {
            var points = LoadRaw(priceArea);

            return points
                .GroupBy(p => (p.TimeDk.DayOfWeek, p.TimeDk.Hour))
                .Select(g => new WeeklyPatternPoint
                {
                    Day = g.Key.DayOfWeek,
                    Hour = g.Key.Hour,
                    AvgPriceKrPerKwh = g.Average(p => p.PriceKrPerKwh),
                    SampleCount = g.Count(),
                })
                .OrderBy(p => p.Day)
                .ThenBy(p => p.Hour)
                .ToList();
        }
    }

    // Projects the next N hours using the typical price for that (day-of-week,
    // hour) combo. Used when live data is unavailable - built from real observed
    // history, so it's a much better guess than a flat/blank fallback.
    public List<PricePoint> EstimateUpcoming(string priceArea, int hours)
    {
        // lock is reentrant on the same thread, so calling GetWeeklyPattern
        // (which also locks) from in here is safe.
        lock (_lock)
        {
            var pattern = GetWeeklyPattern(priceArea);
            if (pattern.Count == 0) return [];

            var byKey = pattern.ToDictionary(p => (p.Day, p.Hour), p => p.AvgPriceKrPerKwh);
            var startHour = DateTime.Now.Date.AddHours(DateTime.Now.Hour);

            var result = new List<PricePoint>();
            for (var i = 0; i < hours; i++)
            {
                var t = startHour.AddHours(i);
                if (!byKey.TryGetValue((t.DayOfWeek, t.Hour), out var price)) continue;

                result.Add(new PricePoint
                {
                    TimeDk = t,
                    TimeUtc = t.ToUniversalTime(),
                    PriceKrPerKwh = price,
                    IsEstimated = true,
                });
            }

            return result;
        }
    }

    public int DaysOfHistory(string priceArea)
    {
        lock (_lock)
        {
            var points = LoadRaw(priceArea);
            if (points.Count == 0) return 0;
            return (int)Math.Ceiling((points.Max(p => p.TimeUtc) - points.Min(p => p.TimeUtc)).TotalDays);
        }
    }
}
