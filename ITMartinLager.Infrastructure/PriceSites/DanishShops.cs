using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITMartinLager.Infrastructure.PriceSites;

// Danish shops added 2026-10-25 (user: "i want all. Also look at busnesses in strøget copenhagen"). Each one is asked
// gently (one or two at a time, answers kept for a day) and its results go through the same "only this product" filter.

/// <summary>Accord - Denmark's biggest seller of used records and CDs (shops on Vestergade by Strøget, Fiolstræde, Nørrebro
/// ...). Its Shopify search tags every copy with condition=Brugt/Nyt and type=CD/Vinyl - only USED copies of the right
/// format count, so a used CD is never priced from a new LP.</summary>
public sealed partial class AccordListings(HttpClient http, ILogger<AccordListings> log)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    public const string Cd = "CD", Vinyl = "Vinyl", Cassette = "Kassette";
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://accord.dk/search?q=" + Uri.EscapeDataString(query);
    private static string ApiUrl(string query) =>
        "https://accord.dk/search/suggest.json?resources%5Btype%5D=product&resources%5Blimit%5D=10&q=" + Uri.EscapeDataString(query);

    public async Task<Listings?> LookupAsync(string title, string artist, string format, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var query = $"{artist} {title}".Trim();
        var key = format + "|" + query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;
        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl(query));
            req.Headers.UserAgent.ParseAdd(Ua);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("Accord {Status} for {Query}", res.StatusCode, query); return null; }
            var result = Parse(await res.Content.ReadAsStringAsync(ct), title, artist, format, SearchUrl(query));
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Accord lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    internal static Listings Parse(string json, string title, string artist, string format, string url)
    {
        var match = ListingParse.Matcher(title, artist);
        var offers = new List<Offer>();
        try
        {
            var products = JsonDocument.Parse(json).RootElement.GetProperty("resources").GetProperty("results").GetProperty("products");
            foreach (var p in products.EnumerateArray())
            {
                var name = p.TryGetProperty("title", out var t) ? WebUtility.HtmlDecode(t.GetString() ?? "") : "";
                var tags = p.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array
                    ? tg.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
                var conditions = tags.Where(x => x.StartsWith("condition=")).ToList();
                // Used only - a copy tagged both used and new is left out, its price could be either.
                if (conditions.Count != 1 || conditions[0] != "condition=Brugt") continue;
                if (!tags.Contains("type=" + format, StringComparer.OrdinalIgnoreCase)) continue;
                if (name == "" || !match.Matches(name)) continue;
                var path = p.TryGetProperty("url", out var u) ? (u.GetString() ?? "").Split('?')[0] : "";
                decimal? kr = p.TryGetProperty("price", out var pr)
                    && decimal.TryParse(pr.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0
                    ? Math.Round(price) : null;
                offers.Add(new Offer(name, kr, path != "" ? "https://accord.dk" + path : url));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        return ListingParse.Summarise(offers, url, match.Ambiguous);
    }
}

/// <summary>Fantask - the comic shop in Sankt Peders Stræde by Strøget, as the new price for comics and albums. Its quick
/// search gives titles and item numbers; the price is on the item page, so at most 3 item pages are read per search.</summary>
public sealed partial class FantaskListings(HttpClient http, ILogger<FantaskListings> log)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private const int MaxPages = 3;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.fantask.dk/search.asp?q=" + Uri.EscapeDataString(query);

    [GeneratedRegex(@"<a href=""(/detail\.asp\?category=[a-z]+&amp;itemid=\d+)"">(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex HitRx();
    [GeneratedRegex(@"itemprop=""price""\s+content=""([\d.,]+)""")]
    private static partial Regex PriceRx();

    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var key = title.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;
        await Gate.WaitAsync(ct);
        try
        {
            var match = ListingParse.Matcher(title, "");
            // Its search wants the words as one phrase ("Amazing Spider-Man Blood Hunt" finds nothing, "Blood Hunt" does):
            // the whole title first, then the part after a colon, then the last three words.
            List<(string Path, string Name)> hits = [];
            foreach (var q in Tries(title))
            {
                var html = await GetAsync("https://www.fantask.dk/services/autosuggest.asp?p=15&c=0&q=" + Uri.EscapeDataString(q), ct);
                if (html is null) return null;
                hits = Hits(html).Where(h => match.Matches(h.Name)).ToList();
                if (hits.Count > 0) break;
            }
            var offers = new List<Offer>();
            foreach (var (path, name) in hits.Take(MaxPages))
            {
                var page = await GetAsync("https://www.fantask.dk" + path, ct);
                decimal? kr = page is not null && PriceRx().Match(page) is { Success: true } m
                    && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0
                    ? Math.Round(price) : null;
                offers.Add(new Offer(name, kr, "https://www.fantask.dk" + path));
            }
            var result = ListingParse.Summarise(offers, SearchUrl(title), match.Ambiguous);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Fantask lookup failed for {Title}: {Message}", title, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    internal static List<string> Tries(string title)
    {
        var t = title.Trim();
        var tries = new List<string> { t };
        var colon = t.IndexOfAny([':', '–']);
        if (colon > 0 && colon < t.Length - 3) tries.Add(t[(colon + 1)..].Trim());
        var words = t.Replace(":", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 3) tries.Add(string.Join(' ', words[^3..]));
        if (words.Length > 2) tries.Add(string.Join(' ', words[^2..]));
        return tries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // "<a href=\"/detail.asp?category=comics&amp;itemid=185053\">Amazing <mark>spider-man</mark>: Blood Hunt <span>(comics)</span></a>"
    internal static List<(string Path, string Name)> Hits(string html) =>
        HitRx().Matches(html).Select(m => (WebUtility.HtmlDecode(m.Groups[1].Value),
            WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(m.Groups[2].Value, @"<span>.*?</span>", ""), "<[^>]+>", "")).Trim())).ToList();

    private async Task<string?> GetAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(Ua);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) { log.LogInformation("Fantask {Status} for {Url}", res.StatusCode, url); return null; }
        // The shop's pages are Windows-1252 (old ASP) - read as such so æ/ø/å match.
        var bytes = await res.Content.ReadAsByteArrayAsync(ct);
        var charset = res.Content.Headers.ContentType?.CharSet;
        return (charset is { Length: > 0 } && !charset.Contains("utf", StringComparison.OrdinalIgnoreCase)
            ? System.Text.Encoding.Latin1 : System.Text.Encoding.UTF8).GetString(bytes);
    }
}
