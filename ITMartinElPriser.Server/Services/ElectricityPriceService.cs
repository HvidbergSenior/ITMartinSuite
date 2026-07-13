using System.Text.Json.Serialization;

namespace ITMartinElPriser.Server.Services;

public sealed class PricePoint
{
    public DateTime TimeUtc { get; set; }
    public DateTime TimeDk { get; set; }
    public double PriceKrPerKwh { get; set; }
}

public sealed class CheapWindow
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double AvgPriceKrPerKwh { get; set; }
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

            lock (_lock)
            {
                if (_cache.TryGetValue(priceArea, out var stale))
                    return stale.Prices;
            }

            return [];
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
                };
            }
        }

        return best;
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
