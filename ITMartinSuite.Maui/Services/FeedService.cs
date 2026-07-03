using System.Text.RegularExpressions;
using System.Xml.Linq;
using ITMartinSuite.Maui.Models;

namespace ITMartinSuite.Maui.Services;

public class FeedService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Dictionary<string, (DateTime At, List<FeedItem> Items)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    // XML namespaces used in RSS feeds
    private static readonly XNamespace Media   = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace Dc      = "http://purl.org/dc/elements/1.1/";

    public async Task<List<FeedItem>> GetItemsAsync(IEnumerable<FeedSource> sources, int maxItems)
    {
        var tasks = sources
            .Where(s => s.Enabled)
            .Select(s => FetchSourceAsync(s));

        var results = await Task.WhenAll(tasks);

        return results
            .SelectMany(x => x)
            .OrderByDescending(x => x.Published)
            .Take(maxItems)
            .ToList();
    }

    private async Task<List<FeedItem>> FetchSourceAsync(FeedSource source)
    {
        if (_cache.TryGetValue(source.Id, out var cached) &&
            DateTime.UtcNow - cached.At < CacheTtl)
            return cached.Items;

        try
        {
            var xml  = await Http.GetStringAsync(source.RssUrl);
            var doc  = XDocument.Parse(xml);
            var items = ParseFeed(doc, source);
            _cache[source.Id] = (DateTime.UtcNow, items);
            return items;
        }
        catch
        {
            return [];
        }
    }

    private static List<FeedItem> ParseFeed(XDocument doc, FeedSource source)
    {
        var items = new List<FeedItem>();

        // RSS 2.0
        var rssItems = doc.Descendants("item");
        foreach (var el in rssItems)
        {
            var title       = el.Element("title")?.Value.Trim() ?? "";
            var link        = el.Element("link")?.Value.Trim()
                           ?? el.Elements().FirstOrDefault(e => e.Name.LocalName == "origLink")?.Value.Trim()
                           ?? "";
            var description = StripHtml(
                el.Element(Content + "encoded")?.Value
                ?? el.Element("description")?.Value
                ?? "");
            var pubDate     = ParseDate(
                el.Element("pubDate")?.Value
                ?? el.Element(Dc + "date")?.Value);
            var image       = ExtractImage(el);

            if (!string.IsNullOrEmpty(title))
                items.Add(new FeedItem
                {
                    Title       = title,
                    Url         = link,
                    Description = description,
                    ImageUrl    = image,
                    SourceName  = source.Name,
                    SourceColor = source.Color,
                    Published   = pubDate,
                });
        }

        // Atom
        if (!items.Any())
        {
            var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
            var entries  = doc.Descendants(ns + "entry");
            foreach (var el in entries)
            {
                var title       = el.Element(ns + "title")?.Value.Trim() ?? "";
                var link        = el.Elements(ns + "link")
                    .FirstOrDefault(l => l.Attribute("rel")?.Value != "alternate" || true)
                    ?.Attribute("href")?.Value ?? "";
                var description = StripHtml(
                    el.Element(ns + "content")?.Value
                    ?? el.Element(ns + "summary")?.Value
                    ?? "");
                var pubDate     = ParseDate(
                    el.Element(ns + "updated")?.Value
                    ?? el.Element(ns + "published")?.Value);
                var image       = ExtractImage(el);

                if (!string.IsNullOrEmpty(title))
                    items.Add(new FeedItem
                    {
                        Title       = title,
                        Url         = link,
                        Description = description,
                        ImageUrl    = image,
                        SourceName  = source.Name,
                        SourceColor = source.Color,
                        Published   = pubDate,
                    });
            }
        }

        return items;
    }

    private static string ExtractImage(XElement el)
    {
        // <media:thumbnail url="..."/>
        var mediaThumbnail = el.Element(Media + "thumbnail")?.Attribute("url")?.Value;
        if (!string.IsNullOrEmpty(mediaThumbnail)) return mediaThumbnail;

        // <media:content url="..." medium="image"/>
        var mediaContent = el.Elements(Media + "content")
            .FirstOrDefault(e => e.Attribute("medium")?.Value == "image" || e.Attribute("type")?.Value?.StartsWith("image") == true)
            ?.Attribute("url")?.Value;
        if (!string.IsNullOrEmpty(mediaContent)) return mediaContent;

        // <enclosure url="..." type="image/..."/>
        var enclosure = el.Element("enclosure");
        if (enclosure?.Attribute("type")?.Value.StartsWith("image") == true)
            return enclosure.Attribute("url")?.Value ?? "";

        // Try <img src="..."> in description HTML
        var html = el.Element("description")?.Value ?? "";
        var match = Regex.Match(html, @"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 300 ? text[..300] + "…" : text;
    }

    private static DateTime ParseDate(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return DateTime.UtcNow;
        if (DateTime.TryParse(raw, out var dt)) return dt;
        // RFC 822 fallback
        if (DateTimeOffset.TryParseExact(raw,
            ["ddd, dd MMM yyyy HH:mm:ss zzz", "ddd, dd MMM yyyy HH:mm:ss GMT"],
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dto))
            return dto.UtcDateTime;
        return DateTime.UtcNow;
    }
}
