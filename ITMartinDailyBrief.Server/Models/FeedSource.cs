namespace ITMartinDailyBrief.Server.Models;

public class FeedSource
{
    public string Id       { get; init; } = Guid.NewGuid().ToString();
    public string Name     { get; init; } = "";
    public string RssUrl   { get; init; } = "";
    public string Color    { get; init; } = "#6B7280";
    public bool   IsPreset { get; init; } = false;
    public bool   Enabled  { get; set; } = true;

    public static FeedSource[] Presets =>
    [
        new() { Id = "tv2ost",     Name = "TV 2 Øst (regional)", RssUrl = "https://www.tv2east.dk/rss",                                    Color = "#E8002D", IsPreset = true },
        new() { Id = "dr",         Name = "DR",         RssUrl = "https://www.dr.dk/nyheder/service/feeds/senestenyt",                    Color = "#FF6B00", IsPreset = true },
        new() { Id = "politiken",  Name = "Politiken",  RssUrl = "https://politiken.dk/rss/senestenyt.rss",                              Color = "#C41E3A", IsPreset = true },
        new() { Id = "berlingske", Name = "Berlingske", RssUrl = "https://www.berlingske.dk/service/rss",                                Color = "#1A3A8F", IsPreset = true },
        new() { Id = "bt",         Name = "BT",         RssUrl = "https://www.bt.dk/bt/seneste/rss",                                     Color = "#F57C00", IsPreset = true },
        new() { Id = "bbc",        Name = "BBC",        RssUrl = "https://feeds.bbci.co.uk/news/rss.xml",                                Color = "#BB1919", IsPreset = true },
    ];
}
