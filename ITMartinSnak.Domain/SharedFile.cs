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
    public const long MaxSize = 25L * 1024 * 1024;   // 25 MB - well under Cloudflare's 100 MB, fine for a PDF or a deck
    public const int MaxFiles = 300, MaxName = 120;

    // Documents, pictures and the usual office files. No programs or scripts.
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
    };

    public static string Allowed => string.Join(",", Types.Keys);

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

    /// <returns>The content type, or null when that kind of file is not allowed.</returns>
    public static string? TypeOf(string fileName) => Types.GetValueOrDefault(Path.GetExtension(fileName));

    /// <returns>An error text in Danish, or null when the file may be added.</returns>
    public static string? Check(string cleanName, long size, int existingCount)
    {
        if (existingCount >= MaxFiles) return "Der er for mange filer i rummet. Slet nogle gamle først.";
        if (size <= 0) return "Filen er tom.";
        if (size > MaxSize) return "Filen er for stor. Den må højst være 25 MB.";
        if (TypeOf(cleanName) is null) return "Den slags fil kan ikke lægges op. Brug fx PDF, Word, Excel, PowerPoint eller et billede.";
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
