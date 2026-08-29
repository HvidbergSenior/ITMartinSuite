using System.Text.RegularExpressions;
using System.Xml.Linq;
using ITMartinDailyBrief.Server.Models;

namespace ITMartinDailyBrief.Server.Services;

public class FeedService
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<FeedService> _log;

    private readonly Dictionary<string, (DateTime At, List<FeedItem> Items)> _cache = new();

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private static readonly XNamespace Media =
        "http://search.yahoo.com/mrss/";

    private static readonly XNamespace Content =
        "http://purl.org/rss/1.0/modules/content/";

    private static readonly XNamespace Dc =
        "http://purl.org/dc/elements/1.1/";

    public FeedService(
        IHttpClientFactory factory,
        ILogger<FeedService> log)
    {
        _factory = factory;
        _log = log;
    }

    public List<FeedItem> CurrentItems { get; private set; } = [];

    public async Task<List<FeedItem>> GetItemsAsync(
        IEnumerable<FeedSource> sources,
        int maxItems)
    {
        var tasks = sources
            .Where(s => s.Enabled)
            .Select(FetchSourceAsync);

        var results = await Task.WhenAll(tasks);

        var items = results
            .SelectMany(x => x)
            .OrderByDescending(x => x.Published)
            .Take(maxItems)
            .ToList();

        CurrentItems = items;

        return items;
    }

    private async Task<List<FeedItem>> FetchSourceAsync(
        FeedSource source)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(source.Id, out var cached) &&
                DateTime.UtcNow - cached.At < CacheTtl)
            {
                return cached.Items;
            }
        }

        try
        {
            var http = _factory.CreateClient("feed");

            var xml = await http.GetStringAsync(source.RssUrl);

            var doc = XDocument.Parse(xml);

            var items = ParseFeed(doc, source);

            lock (_cache)
            {
                _cache[source.Id] =
                    (DateTime.UtcNow, items);
            }

            return items;
        }
        catch (Exception ex)
        {
            _log.LogWarning(
                "Feed {Name} failed: {Msg}",
                source.Name,
                ex.Message);

            return [];
        }
    }

    private static List<FeedItem> ParseFeed(
        XDocument doc,
        FeedSource source)
    {
        var items = new List<FeedItem>();

        // ------------------------------------------------------------
        // RSS 2.0
        // ------------------------------------------------------------

        foreach (var el in doc.Descendants("item"))
        {
            var title =
                el.Element("title")?.Value.Trim() ?? "";

            var link =
                el.Element("link")?.Value.Trim()
                ?? el.Elements()
                    .FirstOrDefault(e =>
                        e.Name.LocalName == "origLink")
                    ?.Value
                ?? "";

            var desc =
                StripHtml(
                    el.Element(Content + "encoded")?.Value
                    ?? el.Element("description")?.Value
                    ?? "");

            var date =
                ParseDate(
                    el.Element("pubDate")?.Value
                    ?? el.Element(Dc + "date")?.Value);

            var img =
                ExtractImage(el);

            var audio =
                ExtractAudioUrl(el);

            if (!string.IsNullOrEmpty(title))
            {
                items.Add(new FeedItem
                {
                    Title = title,
                    Url = link,
                    Description = desc,
                    ImageUrl = img,
                    AudioUrl = audio,
                    SourceName = source.Name,
                    SourceColor = source.Color,
                    Published = date
                });
            }
        }

        // ------------------------------------------------------------
        // Atom fallback
        // ------------------------------------------------------------

        if (items.Count == 0)
        {
            var ns =
                doc.Root?.Name.Namespace
                ?? XNamespace.None;

            foreach (var el in doc.Descendants(ns + "entry"))
            {
                var title =
                    el.Element(ns + "title")?.Value.Trim()
                    ?? "";

                var link =
                    el.Elements(ns + "link")
                        .FirstOrDefault()
                        ?.Attribute("href")
                        ?.Value
                    ?? "";

                var desc =
                    StripHtml(
                        el.Element(ns + "content")?.Value
                        ?? el.Element(ns + "summary")?.Value
                        ?? "");

                var date =
                    ParseDate(
                        el.Element(ns + "updated")?.Value
                        ?? el.Element(ns + "published")?.Value);

                var img =
                    ExtractImage(el);

                var audio =
                    ExtractAudioUrl(el);

                if (!string.IsNullOrEmpty(title))
                {
                    items.Add(new FeedItem
                    {
                        Title = title,
                        Url = link,
                        Description = desc,
                        ImageUrl = img,
                        AudioUrl = audio,
                        SourceName = source.Name,
                        SourceColor = source.Color,
                        Published = date
                    });
                }
            }
        }

        return items;
    }

    // ------------------------------------------------------------
    // Audio
    // ------------------------------------------------------------

    private static string ExtractAudioUrl(XElement el)
    {
        // Standard RSS enclosure:
        //
        // <enclosure
        //     url="https://..."
        //     type="audio/mpeg"
        //     length="..." />

        var enclosure =
            el.Element("enclosure");

        if (enclosure != null)
        {
            var url =
                enclosure.Attribute("url")?.Value ?? "";

            var type =
                enclosure.Attribute("type")?.Value ?? "";

            if (!string.IsNullOrEmpty(url))
            {
                if (type.StartsWith(
                        "audio/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                // Some feeds don't specify type correctly.
                if (LooksLikeAudio(url))
                    return url;
            }
        }

        // Some feeds use media:content for audio.
        var mediaContent =
            el.Elements(Media + "content")
                .FirstOrDefault(e =>
                {
                    var medium =
                        e.Attribute("medium")?.Value ?? "";

                    var type =
                        e.Attribute("type")?.Value ?? "";

                    var url =
                        e.Attribute("url")?.Value ?? "";

                    return
                        medium.Equals(
                            "audio",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        type.StartsWith(
                            "audio/",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        LooksLikeAudio(url);
                });

        if (mediaContent != null)
        {
            return mediaContent
                .Attribute("url")?.Value ?? "";
        }

        return "";
    }

    private static bool LooksLikeAudio(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var lower =
            url.ToLowerInvariant();

        return
            lower.Contains(".mp3")
            || lower.Contains(".m4a")
            || lower.Contains(".aac")
            || lower.Contains(".ogg")
            || lower.Contains(".wav")
            || lower.Contains(".opus")
            || lower.Contains(".m4b");
    }

    // ------------------------------------------------------------
    // Images
    // ------------------------------------------------------------

    private static string ExtractImage(XElement el)
    {
        var thumb =
            el.Element(Media + "thumbnail")
                ?.Attribute("url")
                ?.Value;

        if (!string.IsNullOrEmpty(thumb))
            return thumb;

        var mc =
            el.Elements(Media + "content")
                .FirstOrDefault(e =>
                    e.Attribute("medium")?.Value == "image"
                    ||
                    e.Attribute("type")?.Value?
                        .StartsWith("image") == true)
                ?.Attribute("url")
                ?.Value;

        if (!string.IsNullOrEmpty(mc))
            return mc;

        var enc =
            el.Element("enclosure");

        if (enc?.Attribute("type")?.Value
            .StartsWith("image") == true)
        {
            return enc.Attribute("url")?.Value ?? "";
        }

        var html =
            el.Element("description")?.Value ?? "";

        var match =
            Regex.Match(
                html,
                @"<img[^>]+src=[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups[1].Value
            : "";
    }

    // ------------------------------------------------------------
    // HTML
    // ------------------------------------------------------------

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        var text =
            Regex.Replace(
                html,
                @"<[^>]+>",
                " ");

        text =
            System.Net.WebUtility.HtmlDecode(text);

        text =
            Regex.Replace(
                text,
                @"\s+",
                " ")
            .Trim();

        return text.Length > 280
            ? text[..280] + "…"
            : text;
    }

    // ------------------------------------------------------------
    // Date
    // ------------------------------------------------------------

    private static DateTime ParseDate(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return DateTime.UtcNow;

        if (DateTime.TryParse(
                raw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dt))
        {
            return dt;
        }

        if (DateTimeOffset.TryParseExact(
                raw,
                [
                    "ddd, dd MMM yyyy HH:mm:ss zzz",
                    "ddd, dd MMM yyyy HH:mm:ss GMT"
                ],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dto))
        {
            return dto.UtcDateTime;
        }

        return DateTime.UtcNow;
    }
}