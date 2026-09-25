using Microsoft.Extensions.Options;

namespace ITMartinUpload.Server.Services;

public sealed record StoredFile(string Name, long Size, DateTime ModifiedUtc);

/// <summary>
/// Everything that touches the disk: where a customer's files live, what a file may be
/// called, and how a name collision is resolved. Kept free of ASP.NET types so the rules
/// can be tested without a web server.
/// </summary>
public sealed class UploadStore
{
    private readonly UploadOptions _options;

    public UploadStore(IOptions<UploadOptions> options) : this(options.Value) { }

    public UploadStore(UploadOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.Root);
    }

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

    public string FolderFor(string slug) =>
        Path.Combine(_options.Root, slug.ToLowerInvariant());

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

    public const string PartSuffix = ".part";

    public async Task<string> SaveAsync(string slug, string safeName, Stream content, CancellationToken ct = default)
    {
        var folder = FolderFor(slug);
        Directory.CreateDirectory(folder);
        var dest = UniquePath(folder, safeName, File.Exists);

        // Written under a temporary name and renamed when complete, so a list taken
        // mid-upload (the event feed polls every few seconds) never shows a half file.
        var part = dest + PartSuffix;
        try
        {
            await using (var file = File.Create(part))
                await content.CopyToAsync(file, ct);
            File.Move(part, dest);
        }
        catch
        {
            try { File.Delete(part); } catch (IOException) { }
            throw;
        }
        return dest;
    }

    /// <summary>
    /// True when this customer already sent a file with the same name and the same size.
    /// Good enough to stop the common "did I send these already?" re-upload without
    /// hashing every byte of a 4 GB video on the customer's phone.
    /// </summary>
    public bool AlreadyHave(string slug, string fileName, long size)
    {
        var safeName = SafeFileName(fileName);
        if (safeName is null) return false;
        return List(slug).Any(f =>
            f.Size == size &&
            string.Equals(f.Name, safeName, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff"];

    public static bool IsImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

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

    /// <summary>The folder a removed file is parked in - nothing is ever really deleted here.</summary>
    public const string TrashFolderName = "_fjernet";

    /// <summary>
    /// Resolves a file the customer refers to by name, inside their own folder only.
    /// Returns null when the name is not one of theirs - never a path outside the folder.
    /// </summary>
    public string? ResolveOwnFile(string slug, string fileName)
    {
        var safeName = SafeFileName(fileName);
        if (safeName is null || !IsValidSlug(slug)) return null;

        var folder = FolderFor(slug);
        var full = Path.GetFullPath(Path.Combine(folder, safeName));
        if (!full.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;

        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// Takes a file out of the customer's folder when they say it should not have been
    /// sent. It is moved into <see cref="TrashFolderName"/>, not deleted: if they regret it,
    /// or the wrong one goes, the file is still on the NAS.
    /// </summary>
    public bool Remove(string slug, string fileName)
    {
        var source = ResolveOwnFile(slug, fileName);
        if (source is null) return false;

        var trash = Path.Combine(FolderFor(slug), TrashFolderName);
        Directory.CreateDirectory(trash);
        var dest = UniquePath(trash, Path.GetFileName(source), File.Exists);
        File.Move(source, dest);
        return true;
    }

    public IReadOnlyList<StoredFile> List(string slug)
    {
        try
        {
            var folder = FolderFor(slug);
            if (!Directory.Exists(folder)) return [];
            return Directory.GetFiles(folder)
                .Where(p => !p.EndsWith(PartSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Select(fi => new StoredFile(fi.Name, fi.Length, fi.LastWriteTimeUtc))
                .ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
