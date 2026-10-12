using System.Security.Cryptography;
using System.Text;

namespace ITMartinDailyBrief.Domain;

public sealed record NewsSource(string Id, string Name, string RssUrl, string Color = NewsSource.Grey, bool IsPreset = false)
{
    public const string Grey = "#6B7280";

    public static readonly NewsSource[] Presets =
    [
        // TV 2's own news feed (user 2026-10-08: "free can be tv2 news without ads")
        new("tv2", "TV 2 Nyheder", "https://feeds.services.tv2.dk/api/feeds/nyheder/rss", "#E8002D", true),
        new("tv2ost", "TV 2 Øst (regional)", "https://www.tv2east.dk/rss", "#E8002D", true),
        new("dr", "DR", "https://www.dr.dk/nyheder/service/feeds/senestenyt", "#FF6B00", true),
        new("politiken", "Politiken", "https://politiken.dk/rss/senestenyt.rss", "#C41E3A", true),
        new("berlingske", "Berlingske", "https://www.berlingske.dk/service/rss", "#1A3A8F", true),
        new("bt", "BT", "https://www.bt.dk/bt/seneste/rss", "#F57C00", true),
        new("bbc", "BBC", "https://feeds.bbci.co.uk/news/rss.xml", "#BB1919", true),
    ];
}

public sealed record NewsItem(string Title, string Url, string Description, string ImageUrl, string AudioUrl,
    string SourceName, string SourceColor, DateTime Published)
{
    // Stable across fetches and servers: the article's own address.
    public string Id { get; } = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(Url)))[..16].ToLowerInvariant();

    public bool HasImage => ImageUrl.Length > 0;
    public bool HasAudio => AudioUrl.Length > 0;

    public string TimeAgo(DateTime now)
    {
        var diff = now - Published.ToUniversalTime();
        if (diff.TotalMinutes < 2) return "lige nu";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} t";
        return Published.ToString("d. MMM");
    }

    // The front page: newest first across all sources, the same story only once, cut at the reader's number.
    public static List<NewsItem> FrontPage(IEnumerable<IEnumerable<NewsItem>> perSource, int max) =>
        perSource.SelectMany(x => x).DistinctBy(i => i.Id).OrderByDescending(i => i.Published).Take(max).ToList();
}
