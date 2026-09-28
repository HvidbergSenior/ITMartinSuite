using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Data;

public sealed class HjemDb(DbContextOptions<HjemDb> options) : DbContext(options)
{
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Media> Media => Set<Media>();
    public DbSet<ChatThread> Threads => Set<ChatThread>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<PushSub> PushSubs => Set<PushSub>();
}

// One row (Id = 1): the page's own texts and the chat switch.
public sealed class Settings
{
    public int Id { get; set; }
    public string Name { get; set; } = "Martin Hvidberg";
    public string Intro { get; set; } = "";
    public string NowText { get; set; } = "";
    public DateTime NowUpdated { get; set; } = DateTime.UtcNow;
    public bool Available { get; set; }

    // "Lige nu" can carry one uploaded picture/film, or a link to a video or live stream.
    // Added after launch: Schema.EnsureColumns adds these to an existing hjem.db.
    public string NowMediaName { get; set; } = "";
    public string NowMediaThumb { get; set; } = "";
    public string NowMediaType { get; set; } = "";
    public string NowLink { get; set; } = "";
}

public enum Visibility { Offentlig = 0, Familie = 1 }

public sealed class Post
{
    public int Id { get; set; }
    public DateTime At { get; set; }          // when it happened (the timeline date)
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public Visibility Visibility { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Media> Media { get; set; } = [];
}

public enum MediaKind { Billede = 0, Video = 1, Lyd = 2, Dokument = 3 }

public sealed class Media
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public MediaKind Kind { get; set; }
    public string StoredName { get; set; } = "";
    public string? ThumbName { get; set; }
    public string ContentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public int Sort { get; set; }
}

public sealed class ChatThread
{
    public int Id { get; set; }
    public string VisitorKey { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastAt { get; set; } = DateTime.UtcNow;
    public bool UnreadForOwner { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public ChatThread Thread { get; set; } = null!;
    public bool FromOwner { get; set; }
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// The owner's phones (and PCs) that get a push when a visitor writes.
public sealed class PushSub
{
    public int Id { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
