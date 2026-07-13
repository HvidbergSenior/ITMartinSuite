using System.Text.Json.Serialization;

namespace ITMartinElPriser.Server.Services;

public sealed class PricePoint
{
    public DateTime TimeUtc { get; set; }
    public DateTime TimeDk { get; set; }
    public double PriceKrPerKwh { get; set; }
    public bool IsEstimated { get; set; }
}

public sealed class CheapWindow
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double AvgPriceKrPerKwh { get; set; }
    public bool IsEstimated { get; set; }
}

public sealed class ElectricityPriceService
{
    private readonly HttpClient _http;
    private readonly ILogger<ElectricityPriceService> _logger;
    private readonly PriceHistoryStore _history;

    // Energi Data Service (Energinet) - free, public, no API key. Prices published
    // in DKK per MWh; divide by 1000 to get the kr/kWh figure people actually see
    // on their electricity bill.
    private const string BaseUrl = "https://api.energidataservice.dk/dataset/Elspotprices";

    private readonly Dictionary<string, (List<PricePoint> Prices, DateTime FetchedAtUtc)> _cache = new();
    private readonly object _lock = new();

    public ElectricityPriceService(HttpClient http, ILogger<ElectricityPriceService> logger, PriceHistoryStore history)
    {
        _http = http;
        _logger = logger;
        _history = history;
    }

    public async Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1")
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(priceArea, out var cached) &&
                DateTime.UtcNow - cached.FetchedAtUtc < TimeSpan.FromMinutes(30))
            {
                return cached.Prices;
            }
        }

        var start = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-ddTHH:mm");
        var filter = Uri.EscapeDataString($"{{\"PriceArea\":[\"{priceArea}\"]}}");
        var url = $"{BaseUrl}?start={start}&filter={filter}&sort=HourUTC%20ASC&limit=200";

        try
        {
            var response = await _http.GetFromJsonAsync<EnergiDataResponse>(url);

            var prices = (response?.Records ?? [])
                .Select(r => new PricePoint
                {
                    TimeUtc = r.HourUTC,
                    TimeDk = r.HourDK,
                    PriceKrPerKwh = r.SpotPriceDKK / 1000.0,
                })
                .OrderBy(p => p.TimeUtc)
                .ToList();

            // Energinet answers 200 OK with an empty array when it simply has
            // nothing published yet for the window - treat that the same as a
            // failure so we fall through to cache/estimate instead of caching
            // "no prices" for the next 30 minutes.
            if (prices.Count == 0)
                throw new InvalidOperationException("Energinet returned no records for this window.");

            lock (_lock)
            {
                _cache[priceArea] = (prices, DateTime.UtcNow);
            }

            // Only merge hours that have already happened - tomorrow's published
            // prices aren't "observed" yet and would skew the weekly pattern.
            _history.Merge(priceArea, prices.Where(p => p.TimeDk <= DateTime.Now).ToList());

            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch electricity prices for {PriceArea}", priceArea);

            // Never show a blank screen: fall back to whatever we last had in
            // memory, then to a typical-week estimate built from history, then
            // to a generic day-shape curve if this is a brand new install.
            lock (_lock)
            {
                if (_cache.TryGetValue(priceArea, out var stale))
                    return stale.Prices;
            }

            var estimated = _history.EstimateUpcoming(priceArea, hours: 48);
            return estimated.Count > 0 ? estimated : FallbackPriceCurve.Generate();
        }
    }

    // Best contiguous window of the given length, from the current hour onward -
    // no point recommending a cheap window that already passed today.
    public CheapWindow? FindCheapestWindow(List<PricePoint> prices, int hours)
    {
        var upcoming = prices.Where(p => p.TimeDk >= DateTime.Now.AddMinutes(-59)).ToList();
        if (upcoming.Count < hours) return null;

        CheapWindow? best = null;

        for (var i = 0; i + hours <= upcoming.Count; i++)
        {
            var slice = upcoming.Skip(i).Take(hours).ToList();
            var avg = slice.Average(p => p.PriceKrPerKwh);

            if (best is null || avg < best.AvgPriceKrPerKwh)
            {
                best = new CheapWindow
                {
                    Start = slice[0].TimeDk,
                    End = slice[^1].TimeDk.AddHours(1),
                    AvgPriceKrPerKwh = avg,
                    IsEstimated = slice.Any(p => p.IsEstimated),
                };
            }
        }

        return best;
    }
}

// Last-resort fallback for a brand new install (or a prolonged outage) that has
// no live data and no history yet to estimate from. A rough but plausible
// Danish day-shape so the app never renders a blank price chart.
internal static class FallbackPriceCurve
{
    private static readonly double[] HourlyMultiplier =
    [
        0.55, 0.5, 0.48, 0.48, 0.5, 0.6,
        0.75, 0.95, 1.05, 1.0, 0.9, 0.85,
        0.8, 0.78, 0.8, 0.85, 0.95, 1.15,
        1.35, 1.3, 1.1, 0.9, 0.75, 0.65,
    ];

    private const double BasePriceKrPerKwh = 1.8;

    public static List<PricePoint> Generate(int hours = 48)
    {
        var start = DateTime.Now;

        return Enumerable.Range(0, hours)
            .Select(i => start.AddHours(i))
            .Select(t => new PricePoint
            {
                TimeDk = t,
                TimeUtc = t.ToUniversalTime(),
                PriceKrPerKwh = Math.Round(BasePriceKrPerKwh * HourlyMultiplier[t.Hour], 2),
                IsEstimated = true,
            })
            .ToList();
    }
}

internal sealed class EnergiDataResponse
{
    [JsonPropertyName("records")]
    public List<EnergiDataRecord>? Records { get; set; }
}

internal sealed class EnergiDataRecord
{
    public DateTime HourUTC { get; set; }
    public DateTime HourDK { get; set; }
    public string PriceArea { get; set; } = "";
    public double SpotPriceDKK { get; set; }
    public double? SpotPriceEUR { get; set; }
}
