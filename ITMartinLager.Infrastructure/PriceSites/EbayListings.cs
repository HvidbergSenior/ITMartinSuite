using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITMartinLager.Infrastructure.PriceSites;

// eBay - through its own Browse API (2026-10-11, user: "I have ebay connection now"). App keys Lager__EbayClientId /
// Lager__EbayClientSecret (Production keyset, photoserver magic.env); an app token (client credentials) is fetched and kept
// until shortly before it runs out. Read-only search, no user login. eBay allows 5,000 searches a day - we stop at
// Lager__EbayPerDay (default 4,500) and the answers are cached for a day. eBay has no Danish site: Lager__EbayMarketplace
// (default EBAY_DE) picks the site, prices are its own currency -> kr with the ECB rate. ISBN = search by GTIN.
public sealed class EbayListings(HttpClient http, IConfiguration cfg, ILogger<EbayListings> log)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();
    private static (string Token, DateTime Until) _token = ("", DateTime.MinValue);
    private static (DateOnly Day, int Count) _used = (DateOnly.MinValue, 0);

    private string Market(string? marketplace) =>
        marketplace is { Length: > 0 } given ? given : cfg["Lager:EbayMarketplace"] is { Length: > 0 } m ? m : "EBAY_DE";

    private static string SiteOf(string marketplace) => marketplace switch
    {
        "EBAY_GB" => "www.ebay.co.uk",
        "EBAY_US" => "www.ebay.com",
        "EBAY_FR" => "www.ebay.fr",
        "EBAY_IT" => "www.ebay.it",
        "EBAY_ES" => "www.ebay.es",
        "EBAY_NL" => "www.ebay.nl",
        _ => "www.ebay.de",
    };

    public string SearchUrl(string query, string? marketplace = null) => $"https://{SiteOf(Market(marketplace))}/sch/i.html?_nkw=" + Uri.EscapeDataString(query);

    // marketplace: "EBAY_US" for American comics and merchandise; null = the configured site (EBAY_DE).
    public async Task<Listings?> LookupAsync(string title, string author, string isbn = "", CancellationToken ct = default, string? marketplace = null)
    {
        var market = Market(marketplace);
        var id = cfg["Lager:EbayClientId"] ?? "";
        var secret = cfg["Lager:EbayClientSecret"] ?? "";
        if (id == "" || secret == "") return null;                  // not set up - shows as "svarede ikke"
        if (string.IsNullOrWhiteSpace(title) && isbn == "") return null;
        var query = isbn != "" ? isbn : Pricing.QueryFor(title, author);
        var key = market + "|" + query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (_used.Day != today) _used = (today, 0);
            if (_used.Count >= (int.TryParse(cfg["Lager:EbayPerDay"], out var max) ? max : 4500)) return null;

            var token = await TokenAsync(id, secret, ct);
            if (token == "") return null;
            var url = "https://api.ebay.com/buy/browse/v1/item_summary/search?limit=50&"
                + (isbn != "" ? "gtin=" : "q=") + Uri.EscapeDataString(query);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("X-EBAY-C-MARKETPLACE-ID", market);
            req.Headers.Add("X-EBAY-C-ENDUSERCTX", "contextualLocation=country=DK");
            _used = (today, _used.Count + 1);
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized) _token = ("", DateTime.MinValue);
            if (!res.IsSuccessStatusCode) { log.LogInformation("eBay {Status} for {Query}", res.StatusCode, query); return null; }
            var json = await res.Content.ReadAsStringAsync(ct);
            var result = await ParseAsync(json, isbn != "" ? "" : title, author, SearchUrl(query, market), ct);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("eBay lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    private async Task<string> TokenAsync(string id, string secret, CancellationToken ct)
    {
        if (_token.Token != "" && _token.Until > DateTime.UtcNow) return _token.Token;
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/identity/v1/oauth2/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(id + ":" + secret)));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "https://api.ebay.com/oauth/api_scope",
        });
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) { log.LogWarning("eBay token {Status} - check Lager__EbayClientId/Secret (Production keyset)", res.StatusCode); return ""; }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var token = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        var secs = doc.RootElement.TryGetProperty("expires_in", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 7200;
        _token = (token, DateTime.UtcNow.AddSeconds(secs - 300));
        return token;
    }

    private async Task<Listings> ParseAsync(string json, string title, string author, string url, CancellationToken ct)
    {
        var rates = new Dictionary<string, decimal?>();
        using var doc = JsonDocument.Parse(json);
        foreach (var it in Items(doc))
        {
            var cur = Price(it).Currency;
            if (cur != "" && !rates.ContainsKey(cur)) rates[cur] = await FxRates.ToDkkAsync(http, cur, log, ct);
        }
        return Parse(json, title, author, url, rates);
    }

    // title "" = searched by ISBN, every hit is the book. Price = buy-now price, else the current bid (a floor, not the final price).
    internal static Listings Parse(string json, string title, string author, string url, IReadOnlyDictionary<string, decimal?> toDkk)
    {
        var byIsbn = title == "";
        var match = ListingParse.Matcher(title, author);
        var offers = new List<Offer>();
        using var doc = JsonDocument.Parse(json);
        foreach (var it in Items(doc))
        {
            var name = Str(it, "title").Trim();
            if (name == "" || (!byIsbn && !match.Matches(name))) continue;
            var href = Str(it, "itemWebUrl");
            if (href == "") href = url;
            var (value, cur) = Price(it);
            decimal? kr = null;
            var original = "";
            if (value is > 0 && cur != "")
            {
                original = ListingParse.Original(value.Value, cur);
                if (toDkk.TryGetValue(cur, out var rate) && rate is not null) kr = Math.Round(value.Value * rate.Value);
            }
            offers.Add(new Offer(name, kr, href, original));
        }
        return ListingParse.Summarise(offers, url, !byIsbn && match.Ambiguous);
    }

    private static IEnumerable<JsonElement> Items(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("itemSummaries", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().ToList()
            : [];

    private static (decimal? Value, string Currency) Price(JsonElement it)
    {
        foreach (var p in new[] { "price", "currentBidPrice" })
            if (it.TryGetProperty(p, out var pr) && pr.ValueKind == JsonValueKind.Object
                && decimal.TryParse(Str(pr, "value"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0)
                return (v, Str(pr, "currency"));
        return (null, "");
    }

    private static string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
