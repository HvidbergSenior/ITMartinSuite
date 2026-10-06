using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ITMartinTilbud.Server.Services;

// One chain's week: how many offers there are, how many involve the shop's app, and what you pay extra without it.
public sealed record ChainGap(
    string Chain, int Offers, int AppOffers, int AppOnly, int Priced, decimal ExtraTotal, decimal ExtraAverage, string Leaflets,
    List<GapExample> Examples);
public sealed record GapExample(string Title, decimal WithoutApp, decimal WithApp);

// "App-kløften" (user 2026-10-06): counted in the chains' own leaflets for this week (Tjek /v2/catalogs + /v2/offers),
// using the same reading of app prices as the search. Leaflets are national, so one count serves everyone; kept 6 hours.
public sealed class AppGap(HttpClient http, IMemoryCache cache, ILogger<AppGap> log)
{
    public async Task<List<ChainGap>> ThisWeekAsync(double lat, double lng, CancellationToken ct)
    {
        var key = $"gap|{Math.Round(lat):F0}|{Math.Round(lng):F0}";
        if (cache.TryGetValue(key, out List<ChainGap>? hit) && hit is not null) return hit;
        var inv = CultureInfo.InvariantCulture;
        var now = DateTimeOffset.UtcNow;
        using var cats = JsonDocument.Parse(await http.GetStringAsync(
            $"https://squid-api.tjek.com/v2/catalogs?r_lat={lat.ToString(inv)}&r_lng={lng.ToString(inv)}&r_radius=30000&limit=100", ct));

        // This week's leaflets of the grocery chains: running now, at most 10 days long, food (not the nonfood leaflet).
        var leaflets = cats.RootElement.EnumerateArray()
            .Select(c => (Id: c.GetProperty("id").GetString()!, Chain: c.GetProperty("dealer").GetProperty("name").GetString() ?? "",
                Label: c.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "",
                Count: c.TryGetProperty("offer_count", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0,
                From: DateTimeOffset.Parse(c.GetProperty("run_from").GetString()!, inv), Till: DateTimeOffset.Parse(c.GetProperty("run_till").GetString()!, inv)))
            .Where(c => TjekOffers.GroceryChains.Contains(c.Chain) && c.Count > 0 && c.From <= now && now <= c.Till
                        && (c.Till - c.From).TotalDays <= 10 && !c.Label.Contains("nonfood", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(c => c.Id).ToList();

        var result = new List<ChainGap>();
        foreach (var chain in leaflets.GroupBy(c => c.Chain))
        {
            var offers = new List<Offer>();
            foreach (var c in chain)
                for (var offset = 0; offset < Math.Min(c.Count, 600); offset += 100)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(await http.GetStringAsync(
                            $"https://squid-api.tjek.com/v2/offers?catalog_id={Uri.EscapeDataString(c.Id)}&limit=100&offset={offset}", ct));
                        offers.AddRange(TjekOffers.Parse(doc.RootElement, now));
                    }
                    catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
                    {
                        log.LogInformation(e, "App gap: leaflet {Id} page {Offset} failed", c.Id, offset);
                    }
                }
            if (offers.Count == 0) continue;
            result.Add(Summarise(chain.Key, offers, string.Join(", ", chain.Select(c => c.Label))));
        }
        result = result.OrderByDescending(r => r.Offers == 0 ? 0 : (double)r.AppOffers / r.Offers).ThenBy(r => r.Chain).ToList();
        cache.Set(key, result, TimeSpan.FromHours(6));
        return result;
    }

    internal static ChainGap Summarise(string chain, List<Offer> offers, string leaflets)
    {
        var app = offers.Where(o => o.AppName is not null).ToList();
        // Without-app minus with-app, where the leaflet says both.
        var priced = app.Select(o => o.AppPrice is { } ap ? (o, Without: o.Price, With: ap)
                                    : o.NoAppPrice is { } np ? (o, Without: np, With: o.Price) : (o, Without: 0m, With: 0m))
            .Where(x => x.Without > x.With && x.With > 0).ToList();
        var total = priced.Sum(x => x.Without - x.With);
        return new ChainGap(chain, offers.Count, app.Count, app.Count(o => o.NeedsApp is not null), priced.Count,
            Math.Round(total, 2), priced.Count == 0 ? 0 : Math.Round(total / priced.Count, 2), leaflets,
            priced.OrderByDescending(x => x.Without - x.With).Take(3).Select(x => new GapExample(x.o.Title, x.Without, x.With)).ToList());
    }
}
