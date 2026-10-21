using System.Text.RegularExpressions;

namespace ITMartinHjem.Domain;

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
    // A picture of the app in use, e.g. "/apps/polstrer.jpg" (user 2026-10-08: a showcase shows the app, not a link).
    public string Picture { get; set; } = "";
    public bool Awake { get; set; }    // false = "sover" on the site even when the container runs (user: not ready yet)
    public int Sort { get; set; }

    // Svar only saves rows that are filled in.
    public bool IsComplete => Name.Trim().Length > 0 && Url.StartsWith("http");
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

// "Min dag": what Martin expects to do (morning) and what it actually became (evening). Day = "yyyy-MM-dd", Danish time.
public sealed class DayNote
{
    public int Id { get; set; }
    public string Day { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Done { get; set; } = "";

    public static string Key(DateTime danish) => danish.ToString("yyyy-MM-dd");
    public bool HasText => Plan.Length > 0 || Done.Length > 0;
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
    // Added after launch: the Infrastructure schema upgrade adds these to an existing hjem.db.
    public string NowMediaName { get; set; } = "";
    public string NowMediaThumb { get; set; } = "";
    public string NowMediaType { get; set; } = "";
    public string NowLink { get; set; } = "";

    // "Min dag" on the Lige nu tab (Svar -> 📌): entries added through the day, each a picture and/or a short line.
    // One line per entry "stored|thumb|yyyy-MM-dd HH:mm|text" (stored/thumb empty for a text-only entry).
    public string NowPhotos { get; set; } = "";

    public const int MaxDayEntries = 300;   // about a month of days; the oldest go (files too)
    private const char LineSep = (char)0x2028;   // a line break inside an entry's text (U+2028)

    public sealed record DayEntry(string Stored, string Thumb, DateTime At, string Text);

    public static Settings FirstRun() => new()
    {
        Id = 1,
        Intro = "Er du interesseret, viser og deler jeg gerne, hvad jeg laver, og mine tanker om produkterne.",
        NowText = "Jeg bygger små apps, der hjælper folk med at gøre det rigtige over for store selskaber. Lige nu: MinElpris – så du kan se, om du betaler for meget for strømmen.",
    };

    public List<DayEntry> Entries() => NowPhotos.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(l => l.Split('|', 4))
        .Where(p => p.Length >= 3)
        .Select(p => new DayEntry(p[0], p[1], DateTime.TryParse(p[2], out var at) ? at : Dk.Now, p.Length > 3 ? p[3].Replace(LineSep, '\n') : ""))
        .OrderByDescending(e => e.At)
        .ToList();

    /// <summary>Keeps the newest <see cref="MaxDayEntries"/>; returns the entries that fell off (their files must go too).</summary>
    public List<DayEntry> SetEntries(IEnumerable<DayEntry> entries)
    {
        var sorted = entries.OrderByDescending(x => x.At).ToList();
        NowPhotos = string.Join('\n', sorted.Take(MaxDayEntries)
            .Select(e => $"{e.Stored}|{e.Thumb}|{e.At:yyyy-MM-dd HH:mm}|{e.Text.Replace("\r", "").Replace('\n', LineSep)}"));
        return sorted.Skip(MaxDayEntries).ToList();
    }

    /// <summary>A file under /nu/ is public only while "Lige nu" or "Min dag" uses it. Null = not ours.</summary>
    public string? ContentTypeOfNowFile(string name)
    {
        if (name.Length == 0) return null;
        if (name == NowMediaThumb) return "image/jpeg";
        if (Entries().Any(p => p.Stored.Length > 0 && (p.Stored == name || p.Thumb == name))) return "image/jpeg";
        return name == NowMediaName ? NowMediaType : null;
    }
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

    public static Post Draft() => new() { At = Dk.Today };
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

    public static MediaKind KindOf(string contentType, string ext) =>
        contentType.StartsWith("image/") ? MediaKind.Billede
        : contentType.StartsWith("video/") || ext is ".mp4" or ".mov" or ".m4v" or ".webm" ? MediaKind.Video
        : contentType.StartsWith("audio/") || ext is ".mp3" or ".m4a" or ".wav" or ".ogg" ? MediaKind.Lyd
        : MediaKind.Dokument;
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

// The addresses of the pages edited in Svar.
public static partial class PageRules
{
    public const string Front = "forside", Week = "denne-uge", Bogshoppen = "bogshoppen";
    // The "I dag" tab (Components/NowCard).
    public const string Today = "hvad-laver-itkolibrien";
    // "Følg samarbejdet" also shows the Bogshoppen story on top.
    public const string Collab = "foelg-samarbejdet";

    [GeneratedRegex(@"[^a-z0-9æøå/\-]")] private static partial Regex NotAllowed();

    public static string NormalizeSlug(string slug) => NotAllowed().Replace(slug.Trim().Trim('/').ToLowerInvariant(), "-");

    // Addresses the app itself answers.
    public static bool IsReserved(string slug) =>
        slug is "admin" or "chat" or "login" or "logout" or "api" or "m" or "f" or "nu" || slug.StartsWith("api/") || slug.StartsWith("kolibri");

    public static string? ParentOf(string slug) => slug.Contains('/') ? slug[..slug.LastIndexOf('/')] : null;

    /// <summary>A picture is public on /f/ only when a page uses it.</summary>
    public static string FileLink(string storedName) => "/f/" + storedName;

    public static string FileContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".mp4" => "video/mp4", ".webm" => "video/webm", ".mov" => "video/quicktime", _ => "image/jpeg",
    };
}

// The top menu, the same everywhere (Blazor layout + /api/menu for the hand-made pilot page):
// Forside first, then the pages marked "Vis i menuen" by their Rækkefølge, with "Mine apps" slotted in
// at MineAppsSort. Since 2026-10-07 (user) the menu is just Forside + Mine apps; Om mig and Bliv pilot are
// choices on the front page, not tabs.
public static class SiteMenu
{
    public const int MineAppsSort = 50;
    // "I dag" tab (user 2026-10-12: the day plan box moves off the front page into its own tab) - the ITKolibrien page.
    public const int TodaySort = 40;  // user 2026-10-05: Forside, Hvad laver ITKolibrien, Om mig, Særlige behov, Priser, Mine apps (50), Følg samarbejdet, Bliv pilot

    public sealed record Item(string Title, string Href, bool Pilot = false);

    public static List<Item> Build(IEnumerable<Page> menuPages)
    {
        var middle = menuPages.Select(p => (Sort: p.Sort, Item: new Item(p.Title, "/" + p.Slug)))
            .Append((Sort: TodaySort, Item: new Item("I dag", "/" + PageRules.Today)))
            .Append((Sort: MineAppsSort, Item: new Item("Mine apps", "/mine-apps")))
            .Append((Sort: MineAppsSort + 1, Item: new Item("Spørgsmål", "/spoergsmaal")))   // Q&A tab (user 2026-10-12: "where is the Q/A?")
            .OrderBy(x => x.Sort).ThenBy(x => x.Item.Title)
            .Select(x => x.Item);
        return [new Item("Forside", "/"), .. middle];   // no emojis in the menu (user 2026-10-12: they look AI-made)
    }
}
