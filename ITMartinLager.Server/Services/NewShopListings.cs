using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITMartinLager.Server.Services;

// Bog & idé - the Danish new price (2026-10-10, user: "if the normal price is 5 kr but on amazon.de it costs 50 kr i
// want to know"). The shop is on Shopify, whose public search (search/suggest.json) answers as JSON - the same call the
// shop's own search box makes. A new price is the ceiling for a used copy, not what a used one sells for. Saxo, Williams
// and Arnold Busck block robots; Pricerunner's robots.txt disallows its search.
public sealed class BogIdeListings(HttpClient http, ILogger<BogIdeListings> log)
{
    private const string Ua = "BogshoppenLager/1.0 (ITMartin@Mensa.dk)";
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.bog-ide.dk/search?q=" + Uri.EscapeDataString(query);
    private static string ApiUrl(string query) =>
        "https://www.bog-ide.dk/search/suggest.json?resources%5Btype%5D=product&resources%5Blimit%5D=10&q=" + Uri.EscapeDataString(query);

    public async Task<Listings?> LookupAsync(string title, string author, string isbn = "", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) && isbn == "") return null;
        var query = isbn != "" ? isbn : DbaListings.QueryFor(title, author);
        var key = query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            var result = await SearchAsync(query, title, author, byIsbn: isbn != "", ct);
            // The ISBN on the book is often an older printing than the one in the shop - then by title.
            if (isbn != "" && result is { Count: 0 } && !string.IsNullOrWhiteSpace(title))
                result = await SearchAsync(DbaListings.QueryFor(title, author), title, author, byIsbn: false, ct);
            if (result is not null) Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Bog & idé lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    private async Task<Listings?> SearchAsync(string query, string title, string author, bool byIsbn, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl(query));
        req.Headers.UserAgent.ParseAdd(Ua);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) { log.LogInformation("Bog & idé {Status} for {Query}", res.StatusCode, query); return null; }
        return Parse(await res.Content.ReadAsStringAsync(ct), byIsbn ? "" : title, author, SearchUrl(query));
    }

    // title "" = searched by ISBN, every hit is the book. The product name holds no author, but its address does
    // ("/products/mort-terry-pratchett-paperback-..."), so name + address are matched together.
    internal static Listings Parse(string json, string title, string author, string url)
    {
        var byIsbn = title == "";
        var match = ListingParse.Matcher(title, author);
        var offers = new List<Offer>();
        JsonElement products;
        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            products = root.GetProperty("resources").GetProperty("results").GetProperty("products");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return ListingParse.Summarise(offers, url);
        }
        foreach (var p in products.EnumerateArray())
        {
            var name = p.TryGetProperty("title", out var t) ? WebUtility.HtmlDecode(t.GetString() ?? "") : "";
            var path = p.TryGetProperty("url", out var u) ? (u.GetString() ?? "").Split('?')[0] : "";
            if (name == "" || (!byIsbn && !match.Matches(name + " " + path.Replace('-', ' ')))) continue;
            decimal? kr = p.TryGetProperty("price", out var pr)
                && decimal.TryParse(pr.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0
                ? Math.Round(price) : null;
            offers.Add(new Offer(name, kr, path != "" ? "https://www.bog-ide.dk" + path : url));
        }
        return ListingParse.Summarise(offers, url, !byIsbn && match.Ambiguous);
    }
}

// Bokbörsen - Sweden's used-book market, many antiquarian shops in one search (2026-10-10). robots.txt allows the
// search. Each result carries schema.org microdata: name, author, price in SEK. Turned into kr with the ECB rate.
public sealed partial class BokborsenListings(HttpClient http, ILogger<BokborsenListings> log)
{
    // They answer 403 to a plain bot name - same browser name as the other price sources.
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.bokborsen.se/?q=" + Uri.EscapeDataString(query);

    public async Task<Listings?> LookupAsync(string title, string author, string isbn = "", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) && isbn == "") return null;
        var query = isbn != "" ? isbn : DbaListings.QueryFor(title, author);
        var key = query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, SearchUrl(query));
            req.Headers.UserAgent.ParseAdd(Ua);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("Bokbörsen {Status} for {Query}", res.StatusCode, query); return null; }
            var html = await res.Content.ReadAsStringAsync(ct);
            var sek = await FxRates.ToDkkAsync(http, "SEK", log, ct);
            var result = Parse(html, isbn != "" ? "" : title, author, SearchUrl(query), sek);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Bokbörsen lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    // title "" = searched by ISBN, every hit is the book.
    internal static Listings Parse(string html, string title, string author, string url, decimal? sekToDkk)
    {
        var byIsbn = title == "";
        var match = ListingParse.Matcher(title, author);
        var offers = new List<Offer>();
        foreach (var block in html.Split("class=\"single-product").Skip(1))
        {
            var names = NameRx().Matches(block).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).ToList();
            if (names.Count == 0) continue;
            var name = names[0] + (names.Count > 1 ? " – " + names[1] : "");   // title, then the author
            if (!byIsbn && !match.Matches(name)) continue;
            var link = LinkRx().Match(block);
            var href = link.Success ? "https://www.bokborsen.se" + WebUtility.HtmlDecode(link.Groups[1].Value) : url;
            decimal? kr = null;
            var original = "";
            var p = PriceRx().Match(block);
            if (p.Success && decimal.TryParse(p.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0)
            {
                original = ListingParse.Original(price, "SEK");
                if (sekToDkk is not null) kr = Math.Round(price * sekToDkk.Value);
            }
            offers.Add(new Offer(name, kr, href, original));
        }
        return ListingParse.Summarise(offers, url, !byIsbn && match.Ambiguous);
    }

    [GeneratedRegex("itemprop=\"name\"\\s+data-expanded-value=\"([^\"]*)\"")]
    private static partial Regex NameRx();

    [GeneratedRegex("href=\"(/view/[^\"]+)\"")]
    private static partial Regex LinkRx();

    [GeneratedRegex("content=\"([0-9]+(?:[.,][0-9]+)?) SEK\"")]
    private static partial Regex PriceRx();
}
