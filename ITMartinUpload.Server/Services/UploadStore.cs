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

    public async Task<string> SaveAsync(string slug, string safeName, Stream content, CancellationToken ct = default)
    {
        var folder = FolderFor(slug);
        Directory.CreateDirectory(folder);
        var dest = UniquePath(folder, safeName, File.Exists);

        await using var file = File.Create(dest);
        await content.CopyToAsync(file, ct);
        return dest;
    }

    public IReadOnlyList<StoredFile> List(string slug)
    {
        try
        {
            var folder = FolderFor(slug);
            if (!Directory.Exists(folder)) return [];
            return Directory.GetFiles(folder)
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Select(fi => new StoredFile(fi.Name, fi.Length, fi.LastWriteTimeUtc))
                .ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
