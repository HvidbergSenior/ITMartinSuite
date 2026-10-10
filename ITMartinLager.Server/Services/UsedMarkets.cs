using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ITMartinLager.Server.Services;

// A used-goods site searched by author's surname + title (2026-10-10, user: "I also want sites that specialize in USED
// products"). One fetch per search, cached a day, a few at a time per site. Asking prices, not sold prices.
// Checked 2026-10-10 and left out: Vinted (robots.txt Disallow: /), antikvariat.net (Cloudflare robot check),
// World of Books (robots disallows /search), Lauritz/Biblio/Better World Books/rebuy (403/429 to robots).
public abstract class UsedMarket(HttpClient http, ILogger log)
{
    protected const string BrowserUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public abstract string Name { get; }
    protected virtual string Currency => "DKK";
    protected virtual string AcceptLanguage => "da-DK,da;q=0.9";
    public abstract string UrlFor(string query);

    // Most of them carry the results as schema.org JSON-LD (name + offers.price/priceCurrency).
    protected virtual Listings Parse(string html, string title, string author, string url, decimal? rate) =>
        ListingParse.Parse(html, title, author, url, cur => cur == "DKK" ? 1m : cur == Currency ? rate : null);

    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var url = UrlFor(DbaListings.QueryFor(title, author));
        if (Cache.TryGetValue(url, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        var gate = Gates.GetOrAdd(Name, _ => new SemaphoreSlim(2, 2));
        await gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(BrowserUa);
            req.Headers.AcceptLanguage.ParseAdd(AcceptLanguage);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("{Name} {Status} for {Url}", Name, res.StatusCode, url); return null; }
            var html = await res.Content.ReadAsStringAsync(ct);
            var rate = await FxRates.ToDkkAsync(http, Currency, log, ct);
            var result = Parse(html, title, author, url, rate);
            Cache[url] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("{Name} lookup failed for {Url}: {Message}", Name, url, ex.Message);
            return null;
        }
        finally { gate.Release(); }
    }
}

// Finn.no, Tori.fi and Blocket.se run on the same platform as DBA - the same search address and JSON-LD. robots.txt
// allows /recommerce/forsale/search on all four.
public sealed class FinnListings(HttpClient http, ILogger<FinnListings> log) : UsedMarket(http, log)
{
    public override string Name => "Finn.no";
    protected override string Currency => "NOK";
    protected override string AcceptLanguage => "nb-NO,nb;q=0.9";
    public override string UrlFor(string query) => "https://www.finn.no/recommerce/forsale/search?q=" + Uri.EscapeDataString(query);
}

public sealed class ToriListings(HttpClient http, ILogger<ToriListings> log) : UsedMarket(http, log)
{
    public override string Name => "Tori.fi";
    protected override string Currency => "EUR";
    protected override string AcceptLanguage => "fi-FI,fi;q=0.9";
    public override string UrlFor(string query) => "https://www.tori.fi/recommerce/forsale/search?q=" + Uri.EscapeDataString(query);
}

public sealed class BlocketListings(HttpClient http, ILogger<BlocketListings> log) : UsedMarket(http, log)
{
    public override string Name => "Blocket";
    protected override string Currency => "SEK";
    protected override string AcceptLanguage => "sv-SE,sv;q=0.9";
    public override string UrlFor(string query) => "https://www.blocket.se/recommerce/forsale/search?q=" + Uri.EscapeDataString(query);
}

// GulogGratis - Denmark's other big classifieds site. robots.txt: Allow: /. Results are a JSON-LD ItemList.
// The search is /s/q-word-word - "/s?q=" is ignored and shows the front page's ads (2026-10-10).
public sealed class GulogGratisListings(HttpClient http, ILogger<GulogGratisListings> log) : UsedMarket(http, log)
{
    public override string Name => "GulogGratis";
    public override string UrlFor(string query) =>
        "https://www.guloggratis.dk/s/q-" + Uri.EscapeDataString(string.Join('-', ListingParse.Words(query)));
}

// Kleinanzeigen - Germany's classifieds (formerly eBay Kleinanzeigen). robots.txt allows the /s-... searches. No
// listing JSON-LD: each ad is an <article> with its title in an image JSON block and the price as "9 €" / "5 € VB".
public sealed partial class KleinanzeigenListings(HttpClient http, ILogger<KleinanzeigenListings> log) : UsedMarket(http, log)
{
    public override string Name => "Kleinanzeigen";
    protected override string Currency => "EUR";
    protected override string AcceptLanguage => "de-DE,de;q=0.9";

    public override string UrlFor(string query) =>
        "https://www.kleinanzeigen.de/s-" + Uri.EscapeDataString(string.Join('-', ListingParse.Words(query))) + "/k0";

    protected override Listings Parse(string html, string title, string author, string url, decimal? rate) =>
        ParseAds(html, title, author, url, rate);

    internal static Listings ParseAds(string html, string title, string author, string url, decimal? eurToDkk)
    {
        var match = ListingParse.Matcher(title, author);
        var offers = new List<Offer>();
        foreach (var block in html.Split("<article ").Skip(1))
        {
            var t = TitleRx().Match(block);
            if (!t.Success) continue;
            var name = WebUtility.HtmlDecode(Regex.Unescape(t.Groups[1].Value)).Trim();
            if (!match.Matches(name)) continue;
            var h = HrefRx().Match(block);
            var link = h.Success ? "https://www.kleinanzeigen.de" + WebUtility.HtmlDecode(h.Groups[1].Value) : url;
            decimal? kr = null;
            var original = "";
            var p = PriceRx().Match(block);
            if (p.Success && decimal.TryParse(p.Groups[1].Value.Replace(".", "").Replace(',', '.'), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var eur) && eur > 0)
            {
                original = ListingParse.Original(eur, "EUR") + (p.Groups[2].Success ? " VB" : "");
                if (eurToDkk is not null) kr = Math.Round(eur * eurToDkk.Value);
            }
            offers.Add(new Offer(name, kr, link, original));
        }
        return ListingParse.Summarise(offers, url, match.Ambiguous);
    }

    [GeneratedRegex("\"title\":\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex TitleRx();

    [GeneratedRegex("data-href=\"([^\"]+)\"")]
    private static partial Regex HrefRx();

    [GeneratedRegex(@">\s*([0-9][0-9.]*(?:,[0-9]+)?)\s*€(\s*VB)?\s*<")]
    private static partial Regex PriceRx();
}
