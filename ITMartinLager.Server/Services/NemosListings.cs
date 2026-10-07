using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace ITMartinLager.Server.Services;

// Nemos Bibliotek - Danish shop for used comics and collector books (user 2026-10-07: "nemosbibliotek ... api for
// brugte priser"). WooCommerce, so its open Store API answers a search as JSON with the shop's own used prices -
// real prices a dealer puts on Tintin, Anders And, Asterix and the like. Counted like the other sources.
public sealed class NemosListings(HttpClient http, ILogger<NemosListings> log)
{
    private const string Ua = "BogshoppenLager/1.0 (ITMartin@Mensa.dk)";
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.nemosbibliotek.dk/?s=" + Uri.EscapeDataString(query) + "&post_type=product";
    private static string ApiUrl(string query) =>
        "https://www.nemosbibliotek.dk/wp-json/wc/store/v1/products?per_page=30&search=" + Uri.EscapeDataString(query);

    // Comics are listed by title ("Tintin 5 – Faraos cigarer"), seldom by author, so the search is the title alone.
    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var query = title.Trim();
        var key = (query + "|" + author).ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl(query));
            req.Headers.UserAgent.ParseAdd(Ua);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("Nemos {Status} for {Query}", res.StatusCode, query); return null; }
            var result = Parse(await res.Content.ReadAsStringAsync(ct), title, author, SearchUrl(query));
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Nemos lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    internal static Listings Parse(string json, string title, string author, string url)
    {
        var match = ListingParse.Matcher(title, author) with { Surname = "" };   // comics carry no author in the name
        var offers = new List<Offer>();
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement; } catch (JsonException) { return ListingParse.Summarise(offers, url); }
        if (root.ValueKind != JsonValueKind.Array) return ListingParse.Summarise(offers, url);
        foreach (var p in root.EnumerateArray())
        {
            var name = p.TryGetProperty("name", out var n) ? WebUtility.HtmlDecode(n.GetString() ?? "") : "";
            if (!match.Matches(name)) continue;
            var link = p.TryGetProperty("permalink", out var l) ? l.GetString() ?? url : url;
            decimal? kr = null;
            if (p.TryGetProperty("prices", out var pr) && pr.TryGetProperty("price", out var price)
                && decimal.TryParse(price.GetString(), out var minor) && minor > 0)
            {
                var unit = pr.TryGetProperty("currency_minor_unit", out var u) ? u.GetInt32() : 2;
                kr = Math.Round(minor / (decimal)Math.Pow(10, unit));
            }
            offers.Add(new Offer(name, kr, link));
        }
        return ListingParse.Summarise(offers, url, match.Ambiguous);
    }
}
