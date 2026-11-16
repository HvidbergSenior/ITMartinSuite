using System.Text.RegularExpressions;

namespace ITMartinSnak.Domain;

// Documents in a room (2026-11-09, user: a club group for Acceleratorkursus Hold 36 "there should be chat and also a way to
// upload documents"). Everyone in the room can add a file and download all of them; the one who added it (same name) or
// Martin can remove it. StoredName is the file on disk - never the name the person typed.
// Topic = one of the room's topics (a workshop), "" = Andet.
public sealed record SharedFile(long Id, string Name, string StoredName, long Size, string ContentType, string By, DateTime At,
    string Topic = "");

public static partial class FileRules
{
    // 2026-11-12 trin 2 (user: "all types, bigger than 25 MB"): 90 MB per file - Cloudflare stops anything over 100 MB on the way in.
    // A room holds at most 2 GB; everything goes when the room is deleted ("files melt with the room").
    public const long MaxSize = 90L * 1024 * 1024, MaxRoomBytes = 2L * 1024 * 1024 * 1024;
    public const int MaxFiles = 500, MaxName = 120;
    public const string TooBig = "Filen er for stor. Den må højst være 90 MB. Er den større, så læg den fx i en delt mappe og skriv linket i chatten.";

    // Any kind of file - except programs and scripts, which could harm the phone or computer that opens them (and web pages,
    // which could run in the browser).
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msix", ".bat", ".cmd", ".com", ".scr", ".pif", ".cpl", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".hta", ".jar", ".apk", ".app", ".dll", ".sys", ".sh", ".reg", ".lnk", ".html", ".htm", ".xhtml", ".svg", ".xml", ".php", ".py",
        ".appx", ".dmg", ".pkg", ".iso", ".msp", ".scf", ".url", ".inf", "",
    };

    // The well-known kinds get their own type (PDF and pictures open in the phone's viewer); the rest is a plain download.
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".txt"] = "text/plain; charset=utf-8",
        [".csv"] = "text/csv; charset=utf-8",
        [".rtf"] = "application/rtf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".zip"] = "application/zip",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".mp4"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".epub"] = "application/epub+zip",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".webm"] = "video/webm",
    };

    [GeneratedRegex(@"[\x00-\x1f""<>:/\\|?*]")] private static partial Regex BadChars();

    /// <summary>The name shown in the list: no path, no odd characters, not too long, the extension kept.</summary>
    public static string CleanName(string? fileName)
    {
        var n = Path.GetFileName((fileName ?? "").Replace('\\', '/').Split('/')[^1]);
        n = BadChars().Replace(n, "_").Trim().Trim('.');
        if (n.Length == 0) n = "fil";
        if (n.Length <= MaxName) return n;
        var ext = Path.GetExtension(n);
        return n[..(MaxName - ext.Length)] + ext;
    }

    /// <returns>The content type, or null when that kind of file is not allowed (a program, a script, no extension).</returns>
    public static string? TypeOf(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (Blocked.Contains(ext)) return null;
        return Types.GetValueOrDefault(ext) ?? "application/octet-stream";
    }

    /// <returns>An error text in Danish, or null when the file may be added.</returns>
    public static string? Check(string cleanName, long size, int existingCount, long roomBytes = 0)
    {
        if (existingCount >= MaxFiles) return "Der er for mange filer i rummet. Slet nogle gamle først.";
        if (size <= 0) return "Filen er tom.";
        if (size > MaxSize) return TooBig;
        if (roomBytes + size > MaxRoomBytes) return "Rummet er fuldt (2 GB). Slet nogle store filer først.";
        if (TypeOf(cleanName) is null) return "Programmer og scripts kan ikke lægges op. Alle andre filer kan – fx PDF, Word, billeder, lyd og video.";
        return null;
    }

    public static SharedFile New(IReadOnlyList<SharedFile> existing, string cleanName, long size, string by, DateTime nowUtc, string topic = "")
    {
        var id = (existing.Count == 0 ? 0 : existing.Max(f => f.Id)) + 1;
        return new SharedFile(id, cleanName, $"{id}{Path.GetExtension(cleanName).ToLowerInvariant()}", size, TypeOf(cleanName)!, by, nowUtc, topic);
    }

    /// <summary>Martin, or the one who added it (same name, case doesn't matter - the room is a trusted circle).</summary>
    public static bool MayDelete(SharedFile f, string? name, bool host) =>
        host || string.Equals(f.By, GroupRules.CleanName(name), StringComparison.OrdinalIgnoreCase);
}
