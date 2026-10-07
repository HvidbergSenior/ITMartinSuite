using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ITMartinLager.Server.Services;

// Amazon.de (user 2026-10-07: "also on amazon deutschland ... so that i can put it on the correct place"). No JSON-LD
// here, so the search result blocks are read: title + the price shown (new or from a marketplace seller, EUR -> kr).
// By ISBN when there is one, else author + title. Amazon may answer with a robot check - then it is "ukendt".
public sealed partial class AmazonDeListings(HttpClient http, ILogger<AmazonDeListings> log)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) => "https://www.amazon.de/s?k=" + Uri.EscapeDataString(query) + "&i=stripbooks";

    public async Task<Listings?> LookupAsync(string title, string author, string isbn, CancellationToken ct = default)
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
            req.Headers.AcceptLanguage.ParseAdd("de-DE,de;q=0.9");
            using var res = await http.SendAsync(req, ct);
            var html = res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync(ct) : "";
            if (html == "" || html.Contains("captcha", StringComparison.OrdinalIgnoreCase))
            {
                log.LogInformation("Amazon.de gave {Status}/robot check for {Query}", res.StatusCode, query);
                return null;
            }
            var eur = await FxRates.ToDkkAsync(http, "EUR", log, ct);
            var result = Parse(html, isbn != "" ? "" : title, SearchUrl(query), eur);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Amazon.de lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    // title "" = searched by ISBN, every hit is the book.
    internal static Listings Parse(string html, string title, string url, decimal? eurToDkk)
    {
        var words = ListingParse.Words(title);
        var prices = new List<decimal>();
        var examples = new List<string>();
        var count = 0;
        foreach (var block in html.Split("data-component-type=\"s-search-result\"").Skip(1))
        {
            var t = TitleRx().Match(block);
            var name = t.Success ? WebUtility.HtmlDecode(t.Groups[1].Value).Trim() : "";
            if (name == "" || (words.Count > 0 && !ListingParse.Matches(name, words))) continue;
            count++;
            if (examples.Count < 3) examples.Add(name);
            var w = WholeRx().Match(block);
            if (!w.Success || eurToDkk is null) continue;
            var f = FractionRx().Match(block);
            var whole = w.Groups[1].Value.Replace(".", "").Replace(",", "");
            if (decimal.TryParse(whole + "." + (f.Success ? f.Groups[1].Value : "0"), NumberStyles.Number, CultureInfo.InvariantCulture, out var eur) && eur > 0)
                prices.Add(Math.Round(eur * eurToDkk.Value));
        }
        return ListingParse.Summarise(count, prices, url, examples);
    }

    [GeneratedRegex("<h2[^>]*>.*?<span[^>]*>([^<]+)</span>", RegexOptions.Singleline)]
    private static partial Regex TitleRx();

    [GeneratedRegex("a-price-whole\">([0-9.,]+)")]
    private static partial Regex WholeRx();

    [GeneratedRegex("a-price-fraction\">([0-9]+)")]
    private static partial Regex FractionRx();
}
