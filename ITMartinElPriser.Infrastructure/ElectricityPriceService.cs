using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

using ITMartinElPriser.Application;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Infrastructure;

public sealed partial class ElectricityPriceService(HttpClient http, ILogger<ElectricityPriceService> logger) : IPriceSource
{
    private const string BaseUrl = "https://api.energidataservice.dk/dataset/DayAheadPrices";

    private readonly Dictionary<string, (List<PricePoint> Prices, DateTime FetchedAtUtc)> _cache = new();
    private readonly object _lock = new();

    // Tomorrow's prices are published around 13:00; poll a little more often
    // in that window so the app (and the notification scheduler) pick them up
    // within minutes rather than half an hour.
    // Publication sometimes slips past 14:00 - so as long as tomorrow is still
    // missing from what we hold, keep polling every 5 minutes for the rest of
    // the day instead of sitting on a stale "no tomorrow" for half an hour.
    private static TimeSpan CacheFor(List<PricePoint> cached)
    {
        var now = DkTime.Now;
        if (now.Hour is 12 or 13) return TimeSpan.FromMinutes(5);
        var tomorrow = DateOnly.FromDateTime(now).AddDays(1);
        var hasTomorrow = cached.Any(p => DateOnly.FromDateTime(p.TimeDk) == tomorrow);
        return now.Hour >= 12 && !hasTomorrow ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(30);
    }

    // Yesterday 00:00 through whatever is published (at most tomorrow 23:45).
    // Returns an empty list - never a made-up curve - when Energinet is down
    // and nothing is cached: the UI says so instead of showing fake prices.
    public async Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1", CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(priceArea, out var cached) && DateTime.UtcNow - cached.FetchedAtUtc < CacheFor(cached.Prices))
                return cached.Prices;
        }

        var start = DkTime.Now.Date.AddDays(-1).ToString("yyyy-MM-ddTHH:mm");
        var filter = Uri.EscapeDataString($"{{\"PriceArea\":[\"{priceArea}\"]}}");
        var url = $"{BaseUrl}?start={start}&filter={filter}&sort=TimeUTC%20ASC&limit=400";

        try
        {
            var response = await http.GetFromJsonAsync<EnergiDataResponse>(url, ct);
            var prices = (response?.Records ?? [])
                .Where(r => r.DayAheadPriceDKK is not null)
                .Select(r => new PricePoint
                {
                    TimeUtc = DateTime.SpecifyKind(r.TimeUTC, DateTimeKind.Utc),
                    TimeDk = DateTime.SpecifyKind(r.TimeDK, DateTimeKind.Unspecified),
                    PriceKrPerKwh = r.DayAheadPriceDKK!.Value / 1000.0,
                })
                .OrderBy(p => p.TimeUtc)
                .ToList();

            if (prices.Count == 0)
                throw new InvalidOperationException("Energinet returned no records for this window.");

            lock (_lock) _cache[priceArea] = (prices, DateTime.UtcNow);
            return prices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch electricity prices for {PriceArea}", priceArea);
            lock (_lock)
                return _cache.TryGetValue(priceArea, out var stale) ? stale.Prices : [];
        }
    }
}

// Whole calendar days further back than the live window - for pricing
// yesterday's consumption. Published prices never change, so a day is
// cached for good once fetched.
public sealed partial class ElectricityPriceService
{
    private readonly Dictionary<string, List<PricePoint>> _days = new();

    public async Task<List<PricePoint>> GetDayAsync(DateOnly date, string priceArea = "DK1", CancellationToken ct = default)
    {
        var key = $"{priceArea}|{date:yyyy-MM-dd}";
        lock (_lock) if (_days.TryGetValue(key, out var d)) return d;

        var live = await GetPricesAsync(priceArea, ct);
        var fromLive = live.Where(p => DateOnly.FromDateTime(p.TimeDk) == date).ToList();
        if (fromLive.Count >= 90) return fromLive;

        var prices = await FetchRangeAsync(date, date.AddDays(1), priceArea, ct);
        if (prices.Count >= 90) lock (_lock) _days[key] = prices;
        return prices;
    }

    // Several whole days in one request - Energinet answers 429 to a burst of
    // per-day calls, so anything that wants history asks for the range once.
    // Fills the per-day cache on the way so later single-day asks are free.
    public async Task<List<PricePoint>> GetRangeAsync(DateOnly fromInclusive, DateOnly toExclusive, string priceArea = "DK1", CancellationToken ct = default)
    {
        var missing = new List<DateOnly>();
        var have = new List<PricePoint>();
        lock (_lock)
            for (var d = fromInclusive; d < toExclusive; d = d.AddDays(1))
                if (_days.TryGetValue($"{priceArea}|{d:yyyy-MM-dd}", out var cached)) have.AddRange(cached);
                else missing.Add(d);
        if (missing.Count == 0) return have.OrderBy(p => p.TimeUtc).ToList();

        var fetched = await FetchRangeAsync(missing.Min(), missing.Max().AddDays(1), priceArea, ct);
        lock (_lock)
            foreach (var g in fetched.GroupBy(p => DateOnly.FromDateTime(p.TimeDk)))
                if (g.Count() >= 90) _days[$"{priceArea}|{g.Key:yyyy-MM-dd}"] = g.OrderBy(p => p.TimeUtc).ToList();
        return have.Concat(fetched).OrderBy(p => p.TimeUtc).ToList();
    }

    private async Task<List<PricePoint>> FetchRangeAsync(DateOnly fromInclusive, DateOnly toExclusive, string priceArea, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"{{\"PriceArea\":[\"{priceArea}\"]}}");
        var limit = Math.Max(200, toExclusive.DayNumber - fromInclusive.DayNumber) * 100;
        var url = $"{BaseUrl}?start={fromInclusive:yyyy-MM-dd}T00:00&end={toExclusive:yyyy-MM-dd}T00:00&filter={filter}&sort=TimeUTC%20ASC&limit={limit}";
        try
        {
            var response = await http.GetFromJsonAsync<EnergiDataResponse>(url, ct);
            return (response?.Records ?? [])
                .Where(r => r.DayAheadPriceDKK is not null)
                .Select(r => new PricePoint
                {
                    TimeUtc = DateTime.SpecifyKind(r.TimeUTC, DateTimeKind.Utc),
                    TimeDk = DateTime.SpecifyKind(r.TimeDK, DateTimeKind.Unspecified),
                    PriceKrPerKwh = r.DayAheadPriceDKK!.Value / 1000.0,
                })
                .Where(p => { var d = DateOnly.FromDateTime(p.TimeDk); return d >= fromInclusive && d < toExclusive; })
                .OrderBy(p => p.TimeUtc).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch prices {From}..{To}", fromInclusive, toExclusive);
            return [];
        }
    }
}

internal sealed class EnergiDataResponse
{
    [JsonPropertyName("records")]
    public List<EnergiDataRecord>? Records { get; set; }
}

internal sealed class EnergiDataRecord
{
    public DateTime TimeUTC { get; set; }
    public DateTime TimeDK { get; set; }
    public string PriceArea { get; set; } = "";
    public double? DayAheadPriceDKK { get; set; }
    public double? DayAheadPriceEUR { get; set; }
}
