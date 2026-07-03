namespace ITMartinSuite.Maui.Models;

public class FeedSource
{
    public string Id       { get; init; } = Guid.NewGuid().ToString();
    public string Name     { get; init; } = "";
    public string RssUrl   { get; init; } = "";
    public string Category { get; init; } = "Nyheder";
    public Color  Color    { get; init; } = Colors.Gray;
    public bool   IsPreset { get; init; } = false;
    public bool   Enabled  { get; set; } = true;

    public static FeedSource[] Presets =>
    [
        new() { Id = "tv2",        Name = "TV 2 Nyheder",  RssUrl = "https://feeds.tv2.dk/nyheder/rss",                                       Color = Color.FromArgb("#E8002D"), IsPreset = true },
        new() { Id = "dr",         Name = "DR Nyheder",    RssUrl = "https://www.dr.dk/nyheder/service/feeds/senestenyt",                       Color = Color.FromArgb("#FF6B00"), IsPreset = true },
        new() { Id = "politiken",  Name = "Politiken",     RssUrl = "https://politiken.dk/rss/breaking.rss",                                    Color = Color.FromArgb("#C41E3A"), IsPreset = true },
        new() { Id = "berlingske", Name = "Berlingske",    RssUrl = "https://www.berlingske.dk/arc/outboundfeeds/rss/section/nyheder/",          Color = Color.FromArgb("#1A237E"), IsPreset = true },
        new() { Id = "bt",         Name = "BT",            RssUrl = "https://www.bt.dk/bt/seneste/rss",                                         Color = Color.FromArgb("#F57C00"), IsPreset = true },
        new() { Id = "bbc",        Name = "BBC News",      RssUrl = "https://feeds.bbci.co.uk/news/rss.xml",                                    Color = Color.FromArgb("#BB1919"), IsPreset = true },
        new() { Id = "reuters",    Name = "Reuters",       RssUrl = "https://feeds.reuters.com/reuters/topNews",                                 Color = Color.FromArgb("#FF8000"), IsPreset = true },
    ];
}
