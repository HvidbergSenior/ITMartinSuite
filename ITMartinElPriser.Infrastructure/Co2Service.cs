using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

using ITMartinElPriser.Application;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Infrastructure;

// Energinet's free CO2 data: CO2Emis = measured (5-min, a few minutes behind),
// CO2EmisProg = forecast for the rest of today. No key needed.
public sealed class Co2Service(HttpClient http, ILogger<Co2Service> logger) : ICo2Source
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

    private (PowerMix? Mix, DateTime At) _mix;

    // PowerSystemRightNow: one row per minute for all of Denmark.
    public async Task<PowerMix?> GetMixAsync()
    {
        if (_mix.Mix is not null && DateTime.UtcNow - _mix.At < TimeSpan.FromMinutes(2)) return _mix.Mix;
        try
        {
            var page = await http.GetFromJsonAsync<MixPage>($"{Api}PowerSystemRightNow?limit=1&sort=Minutes1DK%20DESC");
            if (page?.Records.FirstOrDefault() is not { } r) return _mix.Mix;
            var mix = new PowerMix(r.Minutes1DK, (r.OffshoreWindPower ?? 0) + (r.OnshoreWindPower ?? 0), r.SolarPower ?? 0,
                (r.ProductionGe100MW ?? 0) + (r.ProductionLt100MW ?? 0), r.Exchange_Sum ?? 0);
            _mix = (mix, DateTime.UtcNow);
            return mix;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Power mix fetch failed");
            return _mix.Mix;
        }
    }

    private sealed class MixPage
    {
        [JsonPropertyName("records")] public List<MixRec> Records { get; set; } = [];
    }

    private sealed class MixRec
    {
        public DateTime Minutes1DK { get; set; }
        public double? OffshoreWindPower { get; set; }
        public double? OnshoreWindPower { get; set; }
        public double? SolarPower { get; set; }
        public double? ProductionGe100MW { get; set; }
        public double? ProductionLt100MW { get; set; }
        public double? Exchange_Sum { get; set; }
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
