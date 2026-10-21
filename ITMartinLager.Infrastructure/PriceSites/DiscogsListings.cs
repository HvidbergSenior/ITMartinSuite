using System.Collections.Concurrent;
using System.Text.Json;

namespace ITMartinLager.Infrastructure.PriceSites;

// Discogs - the record collectors' market (user 2026-10-12: "also cds, dvds etc"). Open API, no key: the search finds
// the CD releases, and each release's marketplace stats say how many copies are for sale and the lowest price, in
// kroner. Unauthenticated callers get 25 requests a minute - so one search + at most 3 releases, one item at a time.
public sealed class DiscogsListings(HttpClient http, ILogger<DiscogsListings> log)
{
    private const string Ua = "BogshoppenLager/1.0 (ITMartin@Mensa.dk)";
    private const int Releases = 3;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    private static string Q(string title, string artist) => $"{artist} {title}".Trim();
    public static string SearchUrl(string title, string artist) =>
        "https://www.discogs.com/search/?type=release&format_exact=CD&q=" + Uri.EscapeDataString(Q(title, artist));

    public async Task<Listings?> LookupAsync(string title, string artist, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var key = Q(title, artist).ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            var search = await GetAsync($"https://api.discogs.com/database/search?type=release&format=CD&per_page=10&q={Uri.EscapeDataString(Q(title, artist))}", ct);
            if (search is null) return null;
            var match = ListingParse.Matcher(title, artist);
            var offers = new List<Offer>();
            int? want = null, have = null;   // the first matching release's collectors (2026-10-21: demand for guld/sølv/bronze)
            foreach (var r in search.Value.GetProperty("results").EnumerateArray())
            {
                if (offers.Count >= Releases) break;
                var name = r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";   // "Artist - Album"
                if (!match.Matches(name) || !r.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetInt64();
                if (want is null && r.TryGetProperty("community", out var cm))
                    (want, have) = (Int(cm, "want"), Int(cm, "have"));
                var stats = await GetAsync($"https://api.discogs.com/marketplace/stats/{id}?curr_abbr=DKK", ct);
                var forSale = stats is { } s && s.TryGetProperty("num_for_sale", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0;
                decimal? kr = stats is { } s2 && s2.TryGetProperty("lowest_price", out var lp) && lp.ValueKind == JsonValueKind.Object
                    && lp.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number ? Math.Round(v.GetDecimal()) : null;
                var year = r.TryGetProperty("year", out var y) ? $" ({y})" : "";
                offers.Add(new Offer($"{name}{year} – {forSale} til salg", forSale > 0 ? kr : null, $"https://www.discogs.com/sell/release/{id}"));
            }
            var result = ListingParse.Summarise(offers, SearchUrl(title, artist), match.Ambiguous) with { Want = want, Have = have };
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Discogs lookup failed for {Query}: {Message}", key, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private async Task<JsonElement?> GetAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(Ua);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) { log.LogInformation("Discogs {Status} for {Url}", res.StatusCode, url); return null; }
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement.Clone();
    }
}
