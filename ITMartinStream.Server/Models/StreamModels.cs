namespace ITMartinStream.Server.Models;

public enum UpdateType { Text, Milestone, Breaking, Poll }

public class PollOption
{
    public string Text { get; set; } = "";
    public int Votes { get; set; }
}

public class StreamUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public UpdateType Type { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsStarred { get; set; }

    // Set when this update is a reply to an earlier card (comment or update) — makes this a
    // real back-and-forth conversation rather than a one-way broadcast. ReplyToText is a short
    // snapshot of the parent's text, captured at reply time, so the reference still makes sense
    // even if the parent is later deleted.
    public Guid? ReplyToId { get; set; }
    public string? ReplyToText { get; set; }

    public Dictionary<string, int> Reactions { get; set; } = new()
    {
        ["👍"] = 0, ["🔥"] = 0, ["💡"] = 0, ["❤️"] = 0
    };
    public List<PollOption> PollOptions { get; set; } = [];
}

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StreamProject
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "🚀";
    public string StatusText { get; set; } = "";

    // YouTube/Twitch watch URL, pasted as-is by the writer. Video hosting/delivery stays with
    // that platform — this app only embeds it and adds comments/updates around it.
    public string? StreamUrl { get; set; }

    public string WriterPin { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<StreamUpdate> Updates { get; set; } = [];
    public List<Comment> PendingComments { get; set; } = [];
}
