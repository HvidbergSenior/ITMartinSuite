namespace ITMartinUpload.Domain;

public sealed record StoredFile(string Name, long Size, DateTime ModifiedUtc);

/// <summary>One album a music customer sent: "David Bowie – Hunky Dory", its tracks and size.</summary>
public sealed record StoredAlbum(string Name, int Tracks, long Size, DateTime ModifiedUtc);

/// <summary>
/// What a customer folder and a file may be called, and how a name collision is resolved. Pure rules - the disk
/// itself is in Infrastructure.
/// </summary>
public static class UploadNames
{
    /// <summary>The folder a removed file is parked in - nothing is ever really deleted here.</summary>
    public const string TrashFolderName = "_fjernet";

    /// <summary>A file being written; renamed when complete, never listed.</summary>
    public const string PartSuffix = ".part";

    public static bool IsValidSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) &&
        slug.Length <= 50 &&
        slug.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    /// <summary>Strips any path the browser sent; returns null when nothing usable is left.</summary>
    public static string? SafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var name = Path.GetFileName(fileName.Replace('\\', '/').Trim());
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..") return null;
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// The Artist/Album folder a music file goes in: the last two folders that were sent, each one
    /// cleaned like a file name, so "Musik/David Bowie/Hunky Dory" and "..\..\etc" can never climb out.
    /// Null when nothing usable is left - the file then lands at the top like before.
    /// </summary>
    public static string? SafeSubfolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        var parts = folder.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SafeFileName)
            .Where(p => p is not null && !string.Equals(p, TrashFolderName, StringComparison.OrdinalIgnoreCase))
            .Select(p => p!)
            .TakeLast(2)
            .ToArray();
        return parts.Length == 0 ? null : Path.Combine(parts);
    }

    /// <summary>
    /// First free name in the folder: "IMG_1.jpg", then "IMG_1_1.jpg", "IMG_1_2.jpg" …
    /// A re-upload never overwrites what the customer sent us earlier.
    /// </summary>
    public static string UniquePath(string folder, string safeName, Func<string, bool> exists)
    {
        var dest = Path.Combine(folder, safeName);
        if (!exists(dest)) return dest;

        var baseName = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        for (var n = 1; ; n++)
        {
            dest = Path.Combine(folder, $"{baseName}_{n}{ext}");
            if (!exists(dest)) return dest;
        }
    }

    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff"];

    private static readonly string[] AudioExtensions = [".flac", ".mp3", ".m4a", ".aac", ".wav", ".wma", ".ogg", ".opus", ".alac"];

    public static bool IsImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static bool IsAudio(string path) =>
        AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static string ContentTypeFor(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" or ".heif" => "image/heic",
            ".mp4" or ".m4v" => "video/mp4",
            ".mov" => "video/quicktime",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
}
