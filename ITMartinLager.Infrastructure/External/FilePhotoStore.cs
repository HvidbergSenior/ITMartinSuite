using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
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
    // below that a part shows nothing the whole picture did not. (The same cut as Vareinfo's box photos.)
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

    // The AI's area is a little loose or tight now and then: a margin so the edges are not cut off - 6 % of THE THING's own
    // size (2026-11-16, user: "why are they so bad? it should only be one product"): it was 3 % of the whole photo, ~47 px on
    // each side of a 1568 px pile photo, wider than a CD spine (~40 px), so every spine came out with its neighbours.
    internal const double Margin = 0.06;

    public async Task<byte[]?> CropAsync(byte[] jpeg, PhotoArea area, CancellationToken ct, PhotoArea? cover = null)
    {
        using var img = Image.Load<Rgb24>(jpeg);
        if (cover is not null) Cover(img, cover);
        double padX = Math.Max(0.003, (area.Right - area.Left) * Margin), padY = Math.Max(0.003, (area.Bottom - area.Top) * Margin);
        int x = (int)(Math.Max(0, area.Left - padX) * img.Width), y = (int)(Math.Max(0, area.Top - padY) * img.Height);
        int r = (int)(Math.Min(1, area.Right + padX) * img.Width), b = (int)(Math.Min(1, area.Bottom + padY) * img.Height);
        if (r - x < 40 || b - y < 40) return null;
        using var part = img.Clone(c => c.Crop(new Rectangle(x, y, r - x, b - y)));
        using var ms = new MemoryStream();
        await part.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 85 }, ct);
        return ms.ToArray();
    }

    // The price sticker off the picture (user 2026-11-15, "1" = cover it, no AI editing): the area, a little bigger, gets
    // the average colour of a thin ring just outside it - a calm patch in the item's own colour instead of "69,-".
    internal static void Cover(Image<Rgb24> img, PhotoArea a)
    {
        int padX = Math.Max(3, (int)((a.Right - a.Left) * img.Width * 0.15)), padY = Math.Max(3, (int)((a.Bottom - a.Top) * img.Height * 0.15));
        int x0 = Math.Max(0, (int)(a.Left * img.Width) - padX), y0 = Math.Max(0, (int)(a.Top * img.Height) - padY);
        int x1 = Math.Min(img.Width - 1, (int)(a.Right * img.Width) + padX), y1 = Math.Min(img.Height - 1, (int)(a.Bottom * img.Height) + padY);
        if (x1 <= x0 || y1 <= y0) return;
        const int ring = 6;
        long r = 0, g = 0, b = 0, n = 0;
        img.ProcessPixelRows(rows =>
        {
            for (var y = Math.Max(0, y0 - ring); y <= Math.Min(img.Height - 1, y1 + ring); y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = Math.Max(0, x0 - ring); x <= Math.Min(img.Width - 1, x1 + ring); x++)
                {
                    if (x >= x0 && x <= x1 && y >= y0 && y <= y1) continue;   // only the ring outside the sticker
                    r += row[x].R; g += row[x].G; b += row[x].B; n++;
                }
            }
            if (n == 0) return;
            var fill = new Rgb24((byte)(r / n), (byte)(g / n), (byte)(b / n));
            for (var y = y0; y <= y1; y++) rows.GetRowSpan(y).Slice(x0, x1 - x0 + 1).Fill(fill);
        });
    }

    public async Task<byte[]> StraightenAsync(byte[] jpeg, double degrees, CancellationToken ct)
    {
        using var img = Image.Load<Rgb24>(jpeg);
        int w = img.Width, h = img.Height;
        img.Mutate(x => x.Rotate((float)degrees));
        // The largest upright rectangle inside the turned photo (for small angles): no white wedges in the corners.
        double a = Math.Abs(degrees) * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        int keepW = (int)(w * cos - h * sin), keepH = (int)(h * cos - w * sin);
        if (keepW > 40 && keepH > 40)
            img.Mutate(x => x.Crop(new Rectangle((img.Width - keepW) / 2, (img.Height - keepH) / 2, keepW, keepH)));
        using var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 }, ct);
        return ms.ToArray();
    }

    public async Task<byte[]> RotateAsync(byte[] jpeg, int degrees, CancellationToken ct)
    {
        using var img = Image.Load<Rgb24>(jpeg);
        img.Mutate(x => x.Rotate(degrees == 270 ? RotateMode.Rotate270 : degrees == 180 ? RotateMode.Rotate180 : RotateMode.Rotate90));
        using var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 }, ct);
        return ms.ToArray();
    }

    public async Task<(int W, int H)?> SizeAsync(string path, CancellationToken ct)
    {
        if (Inside(path) is not { } full || !File.Exists(full)) return null;
        try { var info = await Image.IdentifyAsync(full, ct); return (info.Width, info.Height); }
        catch (Exception e) when (e is not OperationCanceledException) { return null; }
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
