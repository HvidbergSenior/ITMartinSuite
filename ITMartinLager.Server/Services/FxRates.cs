using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace ITMartinLager.Server.Services;

// kr per USD/EUR/... from the ECB via frankfurter.dev (free, no key), cached a day. Null when it cannot be fetched -
// then foreign prices are left out, never guessed.
public static class FxRates
{
    private static readonly ConcurrentDictionary<string, (DateTime At, decimal Rate)> Cache = new();

    public static async Task<decimal?> ToDkkAsync(HttpClient http, string currency, ILogger log, CancellationToken ct)
    {
        if (currency == "DKK") return 1m;
        if (Cache.TryGetValue(currency, out var r) && r.At > DateTime.UtcNow.AddDays(-1)) return r.Rate;
        try
        {
            var fx = await http.GetFromJsonAsync<Fx>($"https://api.frankfurter.dev/v1/latest?base={currency}&symbols=DKK", ct);
            if (fx?.Rates is not null && fx.Rates.TryGetValue("DKK", out var rate) && rate > 0)
            {
                Cache[currency] = (DateTime.UtcNow, rate);
                return rate;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Exchange rate {Currency} failed: {Message}", currency, ex.Message);
        }
        return null;
    }

    private sealed class Fx { public Dictionary<string, decimal>? Rates { get; set; } }
}
