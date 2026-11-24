using System.Net;
using System.Net.Sockets;
using AngleSharp.Html.Parser;
using ITMartinDailyBrief.Application;
using ITMartinDailyBrief.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinDailyBrief.Infrastructure;

// Fetches the article page and keeps only the body text (paragraphs inside <article>/<main>), no menus, ads or scripts.
// Only public web addresses: article links come from RSS feeds, and readers can add their own feeds,
// so addresses that point into the home network (10.x, 192.168.x, localhost ...) are never fetched.
public sealed class HtmlArticleReader(HttpClient http, ILogger<HtmlArticleReader> log) : IArticleTextReader
{
    private const int MaxBytes = 3_000_000, MinParagraph = 40, MaxRedirects = 4;
    private static readonly string[] Noise = ["script", "style", "noscript", "nav", "header", "footer", "aside", "form", "figure", "button", "svg"];

    public async Task<ArticleText> ReadAsync(NewsItem item, CancellationToken ct)
    {
        try
        {
            if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)) return ArticleText.None;
            HttpResponseMessage? res = null;
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                if (!await IsPublicAsync(uri, ct)) return ArticleText.None;
                res?.Dispose();
                res = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)res.StatusCode is < 300 or > 399 || res.Headers.Location is null) break;
                uri = new Uri(uri, res.Headers.Location);
            }
            using var _ = res;
            if (res is null || !res.IsSuccessStatusCode || res.Content.Headers.ContentLength > MaxBytes) return ArticleText.None;
            if (res.Content.Headers.ContentType?.MediaType is { } type && !type.Contains("html")) return ArticleText.None;
            var html = await res.Content.ReadAsStringAsync(ct);
            return Extract(html.Length > MaxBytes ? html[..MaxBytes] : html, item.Title);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning("Article text failed for {Host}: {Msg}", Uri.TryCreate(item.Url, UriKind.Absolute, out var u) ? u.Host : "?", ex.Message);
            return ArticleText.None;
        }
    }

    // Pages often hold several stories (the one we want + teasers or a live list of others). Take the largest block
    // that is about THIS headline (heading + text); the whole page only as a last resort, and also only when it matches.
    public static ArticleText Extract(string html, string title)
    {
        var doc = new HtmlParser().ParseDocument(html);

        // 1) The page's own data block (JSON-LD articleBody, e.g. TV 2 Øst): clean text, no menus.
        foreach (var script in doc.QuerySelectorAll("script[type='application/ld+json']"))
            if (JsonLdBody(script.TextContent) is { IsWhole: true } fromData && ArticleText.IsAbout(fromData.Text, title)) return fromData;

        foreach (var el in doc.QuerySelectorAll(string.Join(",", Noise)).ToList()) el.Remove();

        // 2) One <article> per story (DR's "seneste nyt" page): the article around the headline that is this story.
        foreach (var h1 in doc.QuerySelectorAll("h1").Where(h => ArticleText.IsAbout(Clean(h.TextContent), title)))
            if (h1.Closest("article") is { } around && ArticleText.From(Paragraphs(around.QuerySelectorAll("p, h2"))) is { IsWhole: true } story)
                return story;

        // 3) The largest article block about this headline (TV 2, BT, BBC ...), counting only its OWN paragraphs:
        //    text inside articles nested in it ("læs også", other stories) does not count. <main> only as a last resort.
        static ArticleText Best(IEnumerable<AngleSharp.Dom.IElement> blocks, string title) => blocks
            .Select(b => ArticleText.From(Paragraphs(b.QuerySelectorAll("p, h2").Where(p => p.ParentElement?.Closest("article") == b.Closest("article")))))
            .Where(t => t.IsWhole && ArticleText.IsAbout(t.Text, title))
            .MaxBy(t => t.Text.Length) ?? ArticleText.None;
        var own = Best(doc.QuerySelectorAll("[itemprop=articleBody], article"), title);
        return own.IsWhole ? own : Best(doc.QuerySelectorAll("main"), title);
    }

    private static ArticleText? JsonLdBody(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var stack = new Stack<System.Text.Json.JsonElement>([doc.RootElement]);
            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (e.ValueKind == System.Text.Json.JsonValueKind.Array) foreach (var x in e.EnumerateArray()) stack.Push(x);
                if (e.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                if (e.TryGetProperty("articleBody", out var body) && body.ValueKind == System.Text.Json.JsonValueKind.String)
                    return ArticleText.From(Clean(new HtmlParser().ParseDocument(body.GetString() ?? "").Body?.TextContent ?? "").Split("\n"));
                foreach (var p in e.EnumerateObject()) stack.Push(p.Value);
            }
        }
        catch (System.Text.Json.JsonException) { }
        return null;
    }

    // Body paragraphs only: links to other stories (a paragraph that is mostly a link) are skipped.
    private static List<string> Paragraphs(IEnumerable<AngleSharp.Dom.IElement> elements) =>
        elements
            .Where(e => e.Closest("a") is null && LinkShare(e) < 0.5)
            .Select(e => Clean(e.TextContent))
            .Where(t => t.Length >= MinParagraph)
            .ToList();

    private static string Clean(string s) => WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ")).Trim();

    private static double LinkShare(AngleSharp.Dom.IElement e)
    {
        var all = e.TextContent.Length;
        return all == 0 ? 0 : e.QuerySelectorAll("a").Sum(a => a.TextContent.Length) / (double)all;
    }

    private static async Task<bool> IsPublicAsync(Uri uri, CancellationToken ct)
    {
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;
        var addresses = IPAddress.TryParse(uri.Host, out var ip) ? [ip] : await Dns.GetHostAddressesAsync(uri.Host, ct);
        return addresses.Length > 0 && addresses.All(IsPublic);
    }

    internal static bool IsPublic(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.Equals(IPAddress.IPv6None));
        var b = ip.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                 || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || b[0] >= 224);
    }
}
