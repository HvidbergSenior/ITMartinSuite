using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ITMartinLager.Infrastructure.PriceSites;

// HipComic - American comic marketplace (user 2026-10-12: "english comic books ... American"). The search page is
// server-rendered: each listing has a title link and a dollar price. Dollars -> kr with the ECB rate.
public sealed partial class HipComicListings(HttpClient http, ILogger<HipComicListings> log)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.hipcomic.com/search?q=" + Uri.EscapeDataString(query);

    // Comics are found by title and issue ("Amazing Spider-Man 300"), not by writer.
    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var query = title.Trim();
        var key = query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, SearchUrl(query));
            req.Headers.UserAgent.ParseAdd(Ua);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("HipComic {Status} for {Query}", res.StatusCode, query); return null; }
            var usd = await FxRates.ToDkkAsync(http, "USD", log, ct);
            var result = Parse(await res.Content.ReadAsStringAsync(ct), title, SearchUrl(query), usd);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("HipComic lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    internal static Listings Parse(string html, string title, string url, decimal? usdToDkk)
    {
        var match = ListingParse.Matcher(title, "");
        var offers = new List<Offer>();
        foreach (var block in html.Split("class=\"r-listing__title\"").Skip(1))
        {
            var link = LinkRx().Match(block);
            var name = NameRx().Match(block);
            if (!link.Success || !name.Success) continue;
            var text = WebUtility.HtmlDecode(name.Groups[1].Value).Trim();
            if (!match.Matches(text)) continue;
            decimal? kr = null;
            var price = PriceRx().Match(block);
            if (price.Success && usdToDkk is { } rate
                && decimal.TryParse(price.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var usd))
                kr = Math.Round(usd * rate);
            offers.Add(new Offer(text, kr, "https://www.hipcomic.com" + link.Groups[1].Value, price.Success ? "$" + price.Groups[1].Value : ""));
        }
        return ListingParse.Summarise(offers, url, match.Ambiguous);
    }

    [GeneratedRegex("href=\"(/listing/[^\"]+)\"")]
    private static partial Regex LinkRx();
    [GeneratedRegex("""<h5[^>]*>([^<]+)</h5>""")]
    private static partial Regex NameRx();
    [GeneratedRegex("""r-listing__price"><span>\s*\$([0-9][0-9,]*(?:\.[0-9]{2})?)""")]
    private static partial Regex PriceRx();
}
