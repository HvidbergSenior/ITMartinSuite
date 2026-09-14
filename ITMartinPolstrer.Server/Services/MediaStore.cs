using ITMartinPolstrer.Server.Data.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartinPolstrer.Server.Services;

// Media lives on disk under the data volume (which is on the NAS when
// deployed), one folder per piece. The app serves it itself from /media,
// so the timeline can show photos inline - no round trip to a separate
// gallery. Photos get a 480px thumbnail at upload time (phone photos are
// 3-5 MB each; a timeline of 40 originals would be unusable on mobile data).
public sealed class MediaStore(IConfiguration config, ILogger<MediaStore> logger)
{
    public string Root { get; } = config["Polstrer:MediaRoot"] ?? "/app/data/media";

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".m4v", ".webm", ".3gp" };

    public sealed record Pending(string OriginalFileName, Stream Content);

    public async Task<MediaFile> SaveAsync(string slug, Pending file, CancellationToken ct = default)
    {
        var dir = Path.Combine(Root, slug);
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
                var thumbName = Path.GetFileNameWithoutExtension(name) + ".thumb.jpg";
                using var img = await Image.LoadAsync(fullPath, ct);
                img.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Size = new Size(480, 480), Mode = ResizeMode.Max }));
                await img.SaveAsJpegAsync(Path.Combine(dir, thumbName), ct);
                thumbRel = $"{slug}/{thumbName}";
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
            RelativePath = $"{slug}/{name}",
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

    public static string Url(string relativePath) => "/media/" + relativePath.Replace('\\', '/');
}
