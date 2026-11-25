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

    // 2026-11-25 (user: the strip on the Elementals card was "just a black rectangle"): the strip was the top 56 px of the photo, and a
    // cover shot on a black table has black rows on top. Now the plain margin is trimmed first (rows/columns with almost no detail),
    // the strip is the top 18 % of what is left, and a strip with no detail at all is not shown.
    internal const double StripShare = 0.18;
    internal const double MarginDetail = 0.25;   // a row/column with less than this share of the typical detail is margin
    internal const double MinStripDetail = 3.0;  // mean neighbour difference (0-255) below which the strip shows nothing

    public async Task<byte[]?> IssueStripAsync(byte[] jpeg, CancellationToken ct)
    {
        using var img = Image.Load<L8>(jpeg);
        var box = ContentBox(img);
        int h = Math.Max(24, (int)(box.Height * StripShare));
        if (box.Width < 60 || box.Height < 80) return null;
        var strip = new Rectangle(box.X, box.Y, box.Width, Math.Min(h, box.Height));
        if (Detail(img, strip) < MinStripDetail) return null;
        using var rgb = Image.Load<Rgb24>(jpeg);
        using var cut = rgb.Clone(c => c.Crop(strip));
        if (cut.Width > 900) cut.Mutate(c => c.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(900, 900) }));
        using var ms = new MemoryStream();
        await cut.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 88 }, ct);
        return ms.ToArray();
    }

    /// <summary>The part of the photo with detail: plain rows/columns around the edge are cut away.</summary>
    internal static Rectangle ContentBox(Image<L8> img)
    {
        var rows = new double[img.Height];
        var cols = new double[img.Width];
        img.ProcessPixelRows(a =>
        {
            for (var y = 0; y < a.Height; y++)
            {
                var row = a.GetRowSpan(y);
                for (var x = 1; x < row.Length; x++)
                {
                    var d = Math.Abs(row[x].PackedValue - row[x - 1].PackedValue);
                    rows[y] += d; cols[x] += d;
                }
                rows[y] /= Math.Max(1, row.Length - 1);
            }
        });
        for (var x = 0; x < cols.Length; x++) cols[x] /= Math.Max(1, img.Height);
        int top = Edge(rows, fromStart: true), bottom = Edge(rows, fromStart: false);
        int left = Edge(cols, fromStart: true), right = Edge(cols, fromStart: false);
        return right - left < 20 || bottom - top < 20 ? new Rectangle(0, 0, img.Width, img.Height)
            : new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    // First (or last) index whose detail is a real share of the typical (median) detail.
    private static int Edge(double[] detail, bool fromStart)
    {
        var sorted = detail.OrderBy(v => v).ToArray();
        var limit = sorted[sorted.Length / 2] * MarginDetail;
        if (fromStart) { for (var i = 0; i < detail.Length; i++) if (detail[i] > limit) return i; return 0; }
        for (var i = detail.Length - 1; i >= 0; i--) if (detail[i] > limit) return i;
        return detail.Length - 1;
    }

    /// <summary>Mean difference between neighbouring pixels inside the area (0-255): 0 = plain colour.</summary>
    internal static double Detail(Image<L8> img, Rectangle area)
    {
        double sum = 0; long n = 0;
        img.ProcessPixelRows(a =>
        {
            for (var y = area.Top; y < area.Bottom && y < a.Height; y++)
            {
                var row = a.GetRowSpan(y);
                for (var x = area.Left + 1; x < area.Right && x < row.Length; x++) { sum += Math.Abs(row[x].PackedValue - row[x - 1].PackedValue); n++; }
            }
        });
        return n == 0 ? 0 : sum / n;
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
