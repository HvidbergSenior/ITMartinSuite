using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Core;

// How much CO2 one kWh from the Danish grid carries, in grams, per 5 minutes.
public sealed record Co2Point(DateTime TimeDk, double Grams);

public sealed record Co2Snapshot(Co2Point? Now, List<Co2Point> Forecast, double? DayAvg);

// Energinet's free CO2 data: CO2Emis = measured (5-min, a few minutes behind),
// CO2EmisProg = forecast for the rest of today. No key needed.
public sealed class Co2Service(HttpClient http, ILogger<Co2Service> logger)
{
    private const string Api = "https://api.energidataservice.dk/dataset/";
    private readonly Dictionary<string, (Co2Snapshot Snap, DateTime At)> _cache = new();
    private readonly object _lock = new();

    public async Task<Co2Snapshot> GetAsync(string area)
    {
        lock (_lock)
            if (_cache.TryGetValue(area, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromMinutes(5)) return c.Snap;
        try
        {
            var filter = Uri.EscapeDataString($"{{\"PriceArea\":[\"{area}\"]}}");
            var measured = await http.GetFromJsonAsync<Page>($"{Api}CO2Emis?limit=1&sort=Minutes5DK%20DESC&filter={filter}");
            var today = DkTime.Now.Date;
            var forecast = await http.GetFromJsonAsync<Page>($"{Api}CO2EmisProg?start={today:yyyy-MM-dd}&filter={filter}&sort=Minutes5DK&limit=0");
            var now = measured?.Records.Select(r => new Co2Point(r.Minutes5DK, r.CO2Emission)).FirstOrDefault();
            var list = forecast?.Records.Select(r => new Co2Point(r.Minutes5DK, r.CO2Emission)).ToList() ?? [];
            var snap = new Co2Snapshot(now, list, list.Count > 0 ? list.Average(p => p.Grams) : null);
            lock (_lock) _cache[area] = (snap, DateTime.UtcNow);
            return snap;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CO2 fetch failed for {Area}", area);
            return new Co2Snapshot(null, [], null);
        }
    }

    private sealed class Page
    {
        [JsonPropertyName("records")] public List<Rec> Records { get; set; } = [];
    }

    private sealed class Rec
    {
        public DateTime Minutes5DK { get; set; }
        public double CO2Emission { get; set; }
    }
}
