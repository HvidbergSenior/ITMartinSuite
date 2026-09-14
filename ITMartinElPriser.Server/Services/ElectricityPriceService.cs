using System.Text.Json.Serialization;

namespace ITMartinElPriser.Server.Services;

// One 15-minute slot of the day-ahead market. The Danish market went from
// hourly to quarter-hourly in 2025; Energinet's old "Elspotprices" dataset
// now returns nothing, "DayAheadPrices" is the live one.
public sealed class PricePoint
{
    public DateTime TimeUtc { get; set; }
    public DateTime TimeDk { get; set; }
    public double PriceKrPerKwh { get; set; }
}

public sealed partial class ElectricityPriceService(HttpClient http, ILogger<ElectricityPriceService> logger)
{
    private const string BaseUrl = "https://api.energidataservice.dk/dataset/DayAheadPrices";

    private readonly Dictionary<string, (List<PricePoint> Prices, DateTime FetchedAtUtc)> _cache = new();
    private readonly object _lock = new();

    // Tomorrow's prices are published around 13:00; poll a little more often
    // in that window so the app (and the notification scheduler) pick them up
    // within minutes rather than half an hour.
    private static TimeSpan CacheFor => DateTime.Now.Hour is 12 or 13 ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(30);

    // Yesterday 00:00 through whatever is published (at most tomorrow 23:45).
    // Returns an empty list - never a made-up curve - when Energinet is down
    // and nothing is cached: the UI says so instead of showing fake prices.
    public async Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1", CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(priceArea, out var cached) && DateTime.UtcNow - cached.FetchedAtUtc < CacheFor)
                return cached.Prices;
        }

        var start = DateTime.Now.Date.AddDays(-1).ToString("yyyy-MM-ddTHH:mm");
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

        var filter = Uri.EscapeDataString($"{{\"PriceArea\":[\"{priceArea}\"]}}");
        var url = $"{BaseUrl}?start={date:yyyy-MM-dd}T00:00&end={date.AddDays(1):yyyy-MM-dd}T00:00&filter={filter}&sort=TimeUTC%20ASC&limit=200";
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
                .Where(p => DateOnly.FromDateTime(p.TimeDk) == date)
                .OrderBy(p => p.TimeUtc).ToList();
            if (prices.Count >= 90) lock (_lock) _days[key] = prices;
            return prices;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch prices for {Date}", date);
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
