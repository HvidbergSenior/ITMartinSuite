using ITMartinHjem.Server.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace ITMartinHjem.Server.Services;

// Files live under <DataDir>/media. Photos are turned upright from EXIF once, at upload
// (the old Olympus sideways-thumbnail trouble must never happen here), and kept in two
// sizes: a 2000 px "full" and an 800 px thumbnail. Everything else is stored as-is.
public sealed class MediaStore
{
    public const long MaxBytes = 500L * 1024 * 1024;
    private readonly string _dir;

    public MediaStore(IConfiguration cfg)
    {
        _dir = Path.Combine(cfg["Hjem:DataDir"] ?? "/app/data", "media");
        Directory.CreateDirectory(_dir);
    }

    public string PathFor(string stored) => Path.Combine(_dir, Path.GetFileName(stored));

    public sealed record Saved(Media Media, DateTime? TakenAt);

    public async Task<Saved> SaveAsync(Stream input, string fileName, string contentType, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N")[..16];
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var kind = KindOf(contentType, ext);

        if (kind == MediaKind.Billede && ext is not ".svg" and not ".gif")
        {
            // ImageSharp needs a seekable stream; browser upload streams are not.
            using var buf = new MemoryStream();
            await input.CopyToAsync(buf, ct);
            buf.Position = 0;
            using var img = await Image.LoadAsync(buf, ct);
            var taken = TakenAt(img);
            img.Mutate(x => x.AutoOrient());
            img.Metadata.ExifProfile = null;
            var enc = new JpegEncoder { Quality = 85 };
            using (var full = img.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(2000, 2000) })))
                await full.SaveAsJpegAsync(PathFor(id + ".jpg"), enc, ct);
            using (var thumb = img.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(800, 800) })))
                await thumb.SaveAsJpegAsync(PathFor(id + "_t.jpg"), enc, ct);
            return new(new Media { Kind = kind, StoredName = id + ".jpg", ThumbName = id + "_t.jpg", ContentType = "image/jpeg", FileName = fileName }, taken);
        }

        await using (var fs = File.Create(PathFor(id + ext)))
            await input.CopyToAsync(fs, ct);
        return new(new Media { Kind = kind, StoredName = id + ext, ContentType = contentType.Length > 0 ? contentType : "application/octet-stream", FileName = fileName }, null);
    }

    public void Delete(Media m)
    {
        foreach (var n in new[] { m.StoredName, m.ThumbName })
            if (!string.IsNullOrEmpty(n) && File.Exists(PathFor(n))) File.Delete(PathFor(n));
    }

    private static MediaKind KindOf(string ct, string ext) =>
        ct.StartsWith("image/") ? MediaKind.Billede
        : ct.StartsWith("video/") || ext is ".mp4" or ".mov" or ".m4v" or ".webm" ? MediaKind.Video
        : ct.StartsWith("audio/") || ext is ".mp3" or ".m4a" or ".wav" or ".ogg" ? MediaKind.Lyd
        : MediaKind.Dokument;

    private static DateTime? TakenAt(Image img)
    {
        var exif = img.Metadata.ExifProfile;
        if (exif is null || !exif.TryGetValue(ExifTag.DateTimeOriginal, out var v) || v?.Value is not { } s) return null;
        return DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }
}
