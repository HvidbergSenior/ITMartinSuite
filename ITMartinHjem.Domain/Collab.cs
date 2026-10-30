using System.Security.Cryptography;
using System.Text;

namespace ITMartinHjem.Domain;

// "Samarbejde" (user 2026-10-30): a private corner on itmartin.dk for Martin and one other person - first Per Nielsen
// (itmartin.dk/per). "Not an artifact to communicate, more a way to share thoughts and upload the texts" - and "music the same way".
// Each room (tekster, musik) holds the files both of them upload, and under every file their thoughts on it.
public sealed class CollabFile
{
    public const int MaxTitle = 120;

    public int Id { get; private set; }
    public string Space { get; private set; } = "";
    public string Room { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string StoredName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public string By { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private CollabFile() { }   // EF

    public static CollabFile Add(string space, string room, string? title, string fileName, string storedName, string contentType,
        long size, string by, DateTime now) => new()
    {
        Space = space, Room = room, Title = CollabRules.TitleFrom(title, fileName), FileName = Path.GetFileName(fileName),
        StoredName = storedName, ContentType = contentType, Size = size, By = by, CreatedAt = now,
    };
}

// A thought on one file - written by Martin or the other person, shown oldest first under the file.
public sealed class CollabNote
{
    public const int MaxText = 4000;

    public int Id { get; private set; }
    public int FileId { get; private set; }
    public string By { get; private set; } = "";
    public string Text { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private CollabNote() { }   // EF

    public static CollabNote Write(int fileId, string by, string? text, DateTime now)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) throw new ArgumentException("Skriv din tanke først.");
        return new CollabNote { FileId = fileId, By = by, Text = t.Length > MaxText ? t[..MaxText] : t, CreatedAt = now };
    }
}

/// <summary>One line in the chat between Martin and the other person (user 2026-10-30: "the chat with per should not be like
/// mail and name... just him, and me like the snak").</summary>
public sealed class CollabMessage
{
    public const int MaxText = 2000;

    public int Id { get; private set; }
    public string Space { get; private set; } = "";
    public string By { get; private set; } = "";
    public string Text { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private CollabMessage() { }   // EF

    public static CollabMessage Write(string space, string by, string? text, DateTime now)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) throw new ArgumentException("Skriv noget først.");
        return new CollabMessage { Space = space, By = by, Text = t.Length > MaxText ? t[..MaxText] : t, CreatedAt = now };
    }
}

/// <summary>One corner: its address (per), the other person's name (Per) and the code that opens it ("" = open to anyone with the link).</summary>
public sealed record CollabSpace(string Slug, string Name, string Code)
{
    public bool Open => Code.Trim().Length == 0;
}

public sealed record CollabRoom(string Slug, string Title, string[] Extensions, long MaxBytes, string Accept);

public static class CollabRules
{
    public const string Texts = "tekster", Music = "musik", Chat = "snak";
    public const string Owner = "Martin";

    public static readonly IReadOnlyList<CollabRoom> Rooms =
    [
        new(Chat, "Snak", [], 0, ""),   // a chat, no files
        new(Texts, "Tekster", [".docx", ".doc", ".odt", ".rtf", ".pdf", ".txt", ".md"], 20_000_000, ".docx,.doc,.odt,.rtf,.pdf,.txt,.md"),
        // 90 MB: Cloudflare stops a single upload at 100 MB.
        new(Music, "Musik", [".mp3", ".m4a", ".wav", ".aac", ".ogg", ".flac", ".mp4", ".mov"], 90_000_000, "audio/*,.m4a,.mp4,.mov"),
    ];

    public static CollabRoom? Room(string? slug) => Rooms.FirstOrDefault(r => r.Slug == (slug ?? "").Trim().ToLowerInvariant());

    /// <summary>"per/tekster" -> ("per", "tekster"); null for any other page address.</summary>
    public static (string Space, string Room)? Split(string? pageSlug)
    {
        var parts = (pageSlug ?? "").Trim('/').ToLowerInvariant().Split('/');
        return parts.Length == 2 && Room(parts[1]) is not null ? (parts[0], parts[1]) : null;
    }

    /// <summary>The cookie role that opens one space (Martin, the owner, opens all of them).</summary>
    public static string RoleFor(string space) => "samarbejde:" + space.Trim().ToLowerInvariant();

    /// <summary>Why the file cannot go in this room, or null when it can.</summary>
    public static string? CheckFile(string room, string? fileName, long size)
    {
        if (Room(room) is not { } r) return "Det rum findes ikke.";
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (size <= 0) return "Vælg en fil først.";
        if (!r.Extensions.Contains(ext)) return $"{r.Title} tager kun disse filer: {string.Join(" ", r.Extensions)}.";
        if (size > r.MaxBytes) return $"Filen er for stor – højst {r.MaxBytes / 1_000_000} MB.";
        return null;
    }

    /// <summary>The title typed, else the file name without its ending ("Af alle farerne der lurer.docx" -> "Af alle farerne der lurer").</summary>
    public static string TitleFrom(string? title, string fileName)
    {
        var t = (title ?? "").Trim();
        if (t.Length == 0) t = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
        if (t.Length == 0) t = "Uden titel";
        return t.Length > CollabFile.MaxTitle ? t[..CollabFile.MaxTitle] : t;
    }

    public static bool IsChat(CollabRoom r) => r.Slug == Chat;

    public static bool IsAudio(CollabFile f) => Path.GetExtension(f.FileName).ToLowerInvariant() is ".mp3" or ".m4a" or ".wav" or ".aac" or ".ogg" or ".flac";
    public static bool IsVideo(CollabFile f) => Path.GetExtension(f.FileName).ToLowerInvariant() is ".mp4" or ".mov";

    public static string SizeText(long bytes) => bytes >= 1_000_000 ? $"{bytes / 1_000_000.0:0.#} MB" : $"{Math.Max(1, bytes / 1000)} kB";

    /// <summary>The code compared in constant time, so the time of a wrong guess says nothing.</summary>
    public static bool CodeFits(CollabSpace space, string? typed) =>
        space.Code.Length > 0 && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes((typed ?? "").Trim().ToLowerInvariant()), Encoding.UTF8.GetBytes(space.Code.Trim().ToLowerInvariant()));
}
