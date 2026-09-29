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
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<AppLink> AppLinks => Set<AppLink>();
    public DbSet<DayNote> DayNotes => Set<DayNote>();
}

// One of Martin's apps on the "Mine apps" page; Show = visible to visitors.
public sealed class AppLink
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
    public string FreeText { get; set; } = "";   // what the free version gives you
    public string PaidText { get; set; } = "";   // what the extended (paid, on request) version adds
    public bool Show { get; set; }
    public bool Awake { get; set; }    // false = "sover" on the site even when the container runs (user: not ready yet)
    public int Sort { get; set; }
}

// A normal web page (Om mig, Kontakt, Ydelser …) - moved in from the one.com builder
// 2026-09-28 and edited in Svar. Slug is the address without the leading slash.
public sealed class Page
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool InMenu { get; set; }
    public int Sort { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// One row (Id = 1): the page's own texts and the chat switch.
// "Min dag": what Martin expects to do (morning) and what it actually became (evening). Day = "yyyy-MM-dd", Danish time.
public sealed class DayNote
{
    public int Id { get; set; }
    public string Day { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Done { get; set; } = "";
}

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

    // "Min dag" on the Lige nu tab (Svar -> 📌): entries added through the day, each a picture and/or a short line.
    // One line per entry "stored|thumb|yyyy-MM-dd HH:mm|text" (stored/thumb empty for a text-only entry).
    public string NowPhotos { get; set; } = "";

    private const char LineSep = '\u2028';   // a line break inside an entry's text

    public sealed record DayEntry(string Stored, string Thumb, DateTime At, string Text);

    public List<DayEntry> Entries() => NowPhotos.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(l => l.Split('|', 4))
        .Where(p => p.Length >= 3)
        .Select(p => new DayEntry(p[0], p[1], DateTime.TryParse(p[2], out var at) ? at : DateTime.Now, p.Length > 3 ? p[3].Replace(LineSep, '\n') : ""))
        .OrderByDescending(e => e.At)
        .ToList();

    public void SetEntries(IEnumerable<DayEntry> entries) =>
        NowPhotos = string.Join('\n', entries.Select(e => $"{e.Stored}|{e.Thumb}|{e.At:yyyy-MM-dd HH:mm}|{e.Text.Replace("\r", "").Replace('\n', LineSep)}"));
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
