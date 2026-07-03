namespace ITMartinDailyBrief.Server.Models;

public class Briefing
{
    public List<Story> Stories     { get; init; } = [];
    public string      Digest      { get; init; } = "";
    public DateTime    GeneratedAt { get; init; } = DateTime.UtcNow;
}

public class Story
{
    public string            Id           { get; init; } = Guid.NewGuid().ToString();
    public string            Headline     { get; init; } = "";
    public string            Summary      { get; init; } = "";
    public string            WhyItMatters { get; init; } = "";
    public string?           Staleness    { get; init; } // null = fresh
    public List<StorySource> Sources      { get; init; } = [];

    public DateTime MostRecent => Sources.Count > 0
        ? Sources.Max(s => s.Published)
        : DateTime.UtcNow;

    public bool IsStale => !string.IsNullOrEmpty(Staleness);
}

public class StorySource
{
    public string   Name      { get; init; } = "";
    public string   Color     { get; init; } = "#6B7280";
    public string   Angle     { get; init; } = "";
    public string   Url       { get; init; } = "";
    public DateTime Published { get; init; }
}
