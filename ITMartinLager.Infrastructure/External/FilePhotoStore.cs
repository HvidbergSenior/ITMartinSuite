using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinLager.Infrastructure.External;

// Photos as files under the data folder (photos/yyyy-MM/..., photos/items/{id}.jpg, photos/kolofon/{id}.jpg).
public sealed class FilePhotoStore(string dataDir) : IPhotoStore
{
    // Claude scales anything larger down to about this anyway; sending more only costs upload time.
    public const int MaxEdge = 1568;

    private readonly string _root = Path.GetFullPath(dataDir);

    // Phone photos are 3-12 MB; Claude only needs ~1568 px, so resize + JPEG before saving and sending.
    public async Task<byte[]> ShrinkAsync(Stream photo, CancellationToken ct)
    {
        using var img = await Image.LoadAsync(photo, ct);
        img.Mutate(x => x.AutoOrient());
        if (img.Width > MaxEdge || img.Height > MaxEdge)
            img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxEdge, MaxEdge) }));
        using var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 82 }, ct);
        return ms.ToArray();
    }

    // 2x2 parts with 12 % overlap, so a book on a cut line is whole in at least one part. Only for photos over 2200 px -
    // below that a part shows nothing the whole picture did not.
    public async Task<List<byte[]>> TilesAsync(Stream photo, CancellationToken ct)
    {
        using var img = await Image.LoadAsync(photo, ct);
        img.Mutate(x => x.AutoOrient());
        if (Math.Max(img.Width, img.Height) < 2200) return [];
        var tiles = new List<byte[]>();
        int w = (int)(img.Width * 0.56), h = (int)(img.Height * 0.56);
        foreach (var (x, y) in new[] { (0, 0), (img.Width - w, 0), (0, img.Height - h), (img.Width - w, img.Height - h) })
        {
            using var part = img.Clone(c => c.Crop(new Rectangle(x, y, w, h)));
            if (part.Width > MaxEdge || part.Height > MaxEdge)
                part.Mutate(c => c.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxEdge, MaxEdge) }));
            using var ms = new MemoryStream();
            await part.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 82 }, ct);
            tiles.Add(ms.ToArray());
        }
        return tiles;
    }

    public async Task SaveAsync(string path, byte[] jpeg, CancellationToken ct)
    {
        var full = Inside(path) ?? throw new InvalidOperationException("Ugyldig sti til billedet.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, jpeg, ct);
    }

    public async Task<byte[]?> ReadAsync(string path, CancellationToken ct) =>
        FullPath(path) is { } full ? await File.ReadAllBytesAsync(full, ct) : null;

    public string? FullPath(string path) => Inside(path) is { } full && File.Exists(full) ? full : null;

    // Never outside the data folder, whatever the path says.
    private string? Inside(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var full = Path.GetFullPath(Path.Combine(_root, path));
        return full.StartsWith(_root + Path.DirectorySeparatorChar) ? full : null;
    }
}
