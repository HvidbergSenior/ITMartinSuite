using System.Collections.Concurrent;
using System.Text.Json;

namespace ITMartinLager.Infrastructure.PriceSites;

// Tradera - Sweden's biggest marketplace, through its own REST API v4 (2026-10-11, our own code - not their plugin).
// App keys Lager__TraderaAppId / Lager__TraderaAppKey (photoserver magic.env). Search is read-only and app-auth only;
// 10,000 calls per method per 24 h - the hunter's 4 a minute stays well under, and answers are cached for a day.
// Price = buy-now price, else the current bid (an auction's bid is a floor, not the final price). SEK -> kr with the ECB rate.
public sealed class TraderaListings(HttpClient http, IConfiguration cfg, ILogger<TraderaListings> log)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.tradera.com/search?q=" + Uri.EscapeDataString(query);

    public async Task<Listings?> LookupAsync(string title, string author, string isbn = "", CancellationToken ct = default)
    {
        var appId = cfg["Lager:TraderaAppId"] ?? "";
        var appKey = cfg["Lager:TraderaAppKey"] ?? "";
        if (appId == "" || appKey == "") return null;               // not set up - shows as "svarede ikke"
        if (string.IsNullOrWhiteSpace(title) && isbn == "") return null;
        var query = isbn != "" ? isbn : Pricing.QueryFor(title, author);
        var key = query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.tradera.com/v4/search?query=" + Uri.EscapeDataString(query));
            req.Headers.Add("X-App-Id", appId);
            req.Headers.Add("X-App-Key", appKey);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("Tradera {Status} for {Query}", res.StatusCode, query); return null; }
            var json = await res.Content.ReadAsStringAsync(ct);
            var sek = await FxRates.ToDkkAsync(http, "SEK", log, ct);
            var result = Parse(json, isbn != "" ? "" : title, author, SearchUrl(query), sek);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Tradera lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    // title "" = searched by ISBN, every hit is the book.
    internal static Listings Parse(string json, string title, string author, string url, decimal? sekToDkk)
    {
        var byIsbn = title == "";
        var match = ListingParse.Matcher(title, author);
        var offers = new List<Offer>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var it in items.EnumerateArray())
            {
                if (it.TryGetProperty("isEnded", out var ended) && ended.ValueKind == JsonValueKind.True) continue;
                var name = Str(it, "shortDescription").Trim();
                if (name == "" || (!byIsbn && !match.Matches(name))) continue;
                var href = Str(it, "itemUrl").Replace("http://", "https://");
                if (href == "") href = url;
                var price = Num(it, "buyItNowPrice") is > 0 and var b ? b : Num(it, "maxBid") is > 0 and var m ? m : Num(it, "nextBid");
                decimal? kr = null;
                var original = "";
                if (price is > 0)
                {
                    original = ListingParse.Original(price.Value, "SEK");
                    if (sekToDkk is not null) kr = Math.Round(price.Value * sekToDkk.Value);
                }
                offers.Add(new Offer(name, kr, href, original));
            }
        return ListingParse.Summarise(offers, url, !byIsbn && match.Ambiguous);
    }

    private static string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static decimal? Num(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
}
