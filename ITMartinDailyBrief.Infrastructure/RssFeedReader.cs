using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ITMartinDailyBrief.Application;
using ITMartinDailyBrief.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinDailyBrief.Infrastructure;

// RSS 2.0, with Atom as fallback. Pictures from media:thumbnail/media:content/enclosure/<img>, sound from enclosure/media:content.
public sealed partial class RssFeedReader(HttpClient http, ILogger<RssFeedReader> log) : IFeedReader
{
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly string[] AudioEndings = [".mp3", ".m4a", ".aac", ".ogg", ".wav", ".opus", ".m4b"];

    public async Task<IReadOnlyList<NewsItem>> ReadAsync(NewsSource source, CancellationToken ct)
    {
        try
        {
            var xml = await http.GetStringAsync(source.RssUrl, ct);
            return Parse(XDocument.Parse(xml), source, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning("Feed {Name} failed: {Msg}", source.Name, ex.Message);
            return [];
        }
    }

    internal static List<NewsItem> Parse(XDocument doc, NewsSource source, DateTime now)
    {
        var items = new List<NewsItem>();
        foreach (var el in doc.Descendants("item"))
        {
            var link = el.Element("link")?.Value.Trim() ?? el.Elements().FirstOrDefault(e => e.Name.LocalName == "origLink")?.Value ?? "";
            Add(el.Element("title")?.Value, link,
                el.Element(Content + "encoded")?.Value ?? el.Element("description")?.Value,
                el.Element("pubDate")?.Value ?? el.Element(Dc + "date")?.Value, el);
        }
        if (items.Count == 0)
        {
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            foreach (var el in doc.Descendants(ns + "entry"))
                Add(el.Element(ns + "title")?.Value, el.Elements(ns + "link").FirstOrDefault()?.Attribute("href")?.Value ?? "",
                    el.Element(ns + "content")?.Value ?? el.Element(ns + "summary")?.Value,
                    el.Element(ns + "updated")?.Value ?? el.Element(ns + "published")?.Value, el);
        }
        return items;

        void Add(string? title, string link, string? html, string? date, XElement el)
        {
            title = (title ?? "").Trim();
            if (title.Length == 0) return;
            items.Add(new NewsItem(title, link, StripHtml(html), Image(el), Audio(el), source.Name, source.Color, ParseDate(date, now)));
        }
    }

    private static string Audio(XElement el)
    {
        if (el.Element("enclosure") is { } enc && enc.Attribute("url")?.Value is { Length: > 0 } url
            && ((enc.Attribute("type")?.Value ?? "").StartsWith("audio/", StringComparison.OrdinalIgnoreCase) || LooksLikeAudio(url)))
            return url;
        return el.Elements(Media + "content").FirstOrDefault(e =>
                (e.Attribute("medium")?.Value ?? "").Equals("audio", StringComparison.OrdinalIgnoreCase)
                || (e.Attribute("type")?.Value ?? "").StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                || LooksLikeAudio(e.Attribute("url")?.Value))
            ?.Attribute("url")?.Value ?? "";
    }

    private static bool LooksLikeAudio(string? url) =>
        !string.IsNullOrWhiteSpace(url) && AudioEndings.Any(e => url.Contains(e, StringComparison.OrdinalIgnoreCase));

    private static string Image(XElement el)
    {
        if (el.Element(Media + "thumbnail")?.Attribute("url")?.Value is { Length: > 0 } thumb) return thumb;
        if (el.Elements(Media + "content").FirstOrDefault(e => e.Attribute("medium")?.Value == "image"
                || e.Attribute("type")?.Value?.StartsWith("image") == true)?.Attribute("url")?.Value is { Length: > 0 } mc) return mc;
        if (el.Element("enclosure") is { } enc && enc.Attribute("type")?.Value?.StartsWith("image") == true) return enc.Attribute("url")?.Value ?? "";
        var m = ImgTag().Match(el.Element("description")?.Value ?? "");
        return m.Success ? m.Groups[1].Value : "";
    }

    internal static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = Spaces().Replace(System.Net.WebUtility.HtmlDecode(Tags().Replace(html, " ")), " ").Trim();
        return text.Length > 280 ? text[..280] + "…" : text;
    }

    private static DateTime ParseDate(string? raw, DateTime now)
    {
        if (string.IsNullOrEmpty(raw)) return now;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
        if (DateTimeOffset.TryParseExact(raw, ["ddd, dd MMM yyyy HH:mm:ss zzz", "ddd, dd MMM yyyy HH:mm:ss GMT"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)) return dto.UtcDateTime;
        return now;
    }

    [GeneratedRegex(@"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase)] private static partial Regex ImgTag();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex Tags();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
}
