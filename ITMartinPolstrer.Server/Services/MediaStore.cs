using ITMartinPolstrer.Server.Data.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartinPolstrer.Server.Services;

// Media lives on disk under the data volume on the NAS, one folder per
// piece, named after the piece so the same folder tree reads well when
// the gallery app mounts it for customers/colleagues:
//
//   media/Lænestol grøn - Marianne/20260914-101500-x1y2z3.jpg
//   media/Lænestol grøn - Marianne/thumbnails/20260914-101500-x1y2z3.jpg
//
// "thumbnails" is the one folder name the gallery already skips, so the
// 480px thumbnails this app uses for its own timeline never show up there
// as duplicate photos.
public sealed class MediaStore(IConfiguration config, ILogger<MediaStore> logger)
{
    public string Root { get; } = config["Polstrer:MediaRoot"] ?? "/app/data/media";

    private const string ThumbDir = "thumbnails";

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".m4v", ".webm", ".3gp" };

    public sealed record Pending(string OriginalFileName, Stream Content);

    // Picks a folder name for a new piece: the title, cleaned of characters
    // a filesystem refuses, made unique with " (2)", " (3)"... if the same
    // title has been used before. Decided once at creation; a later rename
    // of the piece does not move the folder.
    public string NewFolderFor(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(title.Trim().Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim(' ', '.');
        while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
        if (clean.Length == 0) clean = "Møbel";
        if (clean.Length > 80) clean = clean[..80].Trim();

        var candidate = clean;
        for (var n = 2; Directory.Exists(Path.Combine(Root, candidate)); n++)
            candidate = $"{clean} ({n})";
        Directory.CreateDirectory(Path.Combine(Root, candidate));
        return candidate;
    }

    public async Task<MediaFile> SaveAsync(string folder, Pending file, CancellationToken ct = default)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.OriginalFileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var isVideo = VideoExtensions.Contains(ext);
        var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(dir, name);

        await using (var fs = new FileStream(fullPath, FileMode.Create))
            await file.Content.CopyToAsync(fs, ct);

        string? thumbRel = null;
        if (!isVideo)
        {
            try
            {
                var thumbName = Path.GetFileNameWithoutExtension(name) + ".jpg";
                Directory.CreateDirectory(Path.Combine(dir, ThumbDir));
                using var img = await Image.LoadAsync(fullPath, ct);
                img.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Size = new Size(480, 480), Mode = ResizeMode.Max }));
                await img.SaveAsJpegAsync(Path.Combine(dir, ThumbDir, thumbName), ct);
                thumbRel = $"{folder}/{ThumbDir}/{thumbName}";
            }
            catch (Exception ex)
            {
                // A HEIC or otherwise unreadable image still gets stored; it
                // just shows full-size instead of as a thumbnail.
                logger.LogWarning(ex, "Thumbnail failed for {File}", name);
            }
        }

        return new MediaFile
        {
            RelativePath = $"{folder}/{name}",
            ThumbPath = thumbRel,
            IsVideo = isVideo
        };
    }

    public void Delete(MediaFile m)
    {
        foreach (var rel in new[] { m.RelativePath, m.ThumbPath })
        {
            if (rel is null) continue;
            var p = Path.Combine(Root, rel);
            try { if (File.Exists(p)) File.Delete(p); }
            catch (Exception ex) { logger.LogWarning(ex, "Delete failed {Path}", p); }
        }
    }

    public static string Url(string relativePath) =>
        "/media/" + string.Join('/', relativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
}
