namespace ITMartinGallery.Domain;

// What the gallery shows, by file extension.
public static class MediaTypes
{
    public static string Ext(string f) => Path.GetExtension(f).ToLowerInvariant();
    public static bool IsImg(string ext) => ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".heic" or ".avif";
    public static bool IsVid(string ext) => ext is ".mp4" or ".mov" or ".mkv" or ".avi" or ".m4v" or ".webm" or ".wmv";
    public static bool IsAud(string ext) => ext is ".mp3" or ".m4a" or ".aac" or ".wav" or ".flac" or ".ogg";
    public static bool IsDoc(string ext) => ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx";
    public static bool IsMedia(string f) { var e = Ext(f); return IsImg(e) || IsVid(e) || IsAud(e) || IsDoc(e); }

    // The birthday page's own, slightly different set (txt/md count as documents there).
    public static string? CelebrationKind(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".heic" or ".avif" => "img",
        ".mp4" or ".mov" or ".m4v" or ".webm" => "vid",
        ".mp3" or ".m4a" or ".aac" or ".wav" or ".ogg" or ".flac" => "aud",
        ".pdf" or ".doc" or ".docx" or ".txt" or ".md" => "doc",
        _ => null,
    };
}

// Paths inside a library. Pure string work - nothing here touches the disk.
public static class LibraryPaths
{
    // Inside the library or the library itself. A bare prefix check let "/library/anna" reach the sibling
    // "/library/anna-old" (found by BrowseTests 2026-10-28), so the root must end at a separator.
    public static bool IsSafe(string p, string r)
    {
        var full = Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    public static bool IsSameDir(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    public static string Rel(string abs, string r) => Path.GetRelativePath(r, abs).Replace("\\", "/");
    public static string NormalizeRel(string rel) => rel == "." ? "" : rel;
}
