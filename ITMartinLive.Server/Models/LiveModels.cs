namespace ITMartinLive.Server.Models;

public enum UpdateType { Text, Score, Position, Poll, Breaking, Video, Summary }

public class PollOption
{
    public string Text  { get; set; } = "";
    public int    Votes { get; set; }
}

public class LiveUpdate
{
    public Guid       Id        { get; set; } = Guid.NewGuid();
    public UpdateType Type      { get; set; }
    public string     Text      { get; set; } = "";
    public string?    VideoPath { get; set; }
    public DateTime   CreatedAt { get; set; } = DateTime.UtcNow;
    public bool       IsStarred { get; set; }
    public Dictionary<string, int> Reactions { get; set; } = new()
    {
        ["👍"] = 0, ["🔥"] = 0, ["😱"] = 0, ["😢"] = 0
    };
    public List<PollOption> PollOptions { get; set; } = [];
}

public class ViewerMessage
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public string   Text      { get; set; } = "";
    public string   Author    { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LiveEvent
{
    public string   Slug        { get; set; } = "";
    public string   Name        { get; set; } = "";
    public string   SportEmoji  { get; set; } = "🏆";
    public string   HeaderText  { get; set; } = "";
    public string   WriterPin   { get; set; } = "";
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public List<LiveUpdate>    Updates         { get; set; } = [];
    public List<ViewerMessage> PendingMessages { get; set; } = [];
    public int ViewerCount { get; set; }
}
