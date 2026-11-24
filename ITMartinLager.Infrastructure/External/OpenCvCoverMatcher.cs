using ITMartinLager.Application;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ITMartinLager.Infrastructure.External;

/// <summary>ORB features + RANSAC (2026-11-24, CoverMatch): both pictures scaled to 600 px, 1500 features each, Lowe's ratio 0.75,
/// homography inliers = points in the same place on both covers. Pictures go through ImageSharp first, so webp works too.</summary>
public sealed class OpenCvCoverMatcher(HttpClient http, ILogger<OpenCvCoverMatcher> log) : ICoverMatcher
{
    private const int MaxBytes = 3 * 1024 * 1024;

    public async Task<int?> MatchAsync(byte[] ourCover, string listingImageUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(listingImageUrl, UriKind.Absolute, out var u) || u.Scheme != "https") return null;
        byte[] theirs;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, u);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0");
            req.Headers.TryAddWithoutValidation("Accept", "image/webp,image/jpeg,image/png;q=0.9");   // no avif: OpenCV cannot read it
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode || res.Content.Headers.ContentLength > MaxBytes) return null;
            theirs = await res.Content.ReadAsByteArrayAsync(ct);
            if (theirs.Length > MaxBytes) return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogInformation("Listing picture not fetched: {Message}", ex.Message);
            return null;
        }
        try { return Inliers(ourCover, theirs); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Cover not compared: {Message}", ex.Message);
            return null;
        }
    }

    public static int? Inliers(byte[] a, byte[] b)
    {
        using var ga = Grey(a);
        using var gb = Grey(b);
        if (ga is null || gb is null) return null;
        using var orb = ORB.Create(1500);
        using var da = new Mat();
        using var db = new Mat();
        orb.DetectAndCompute(ga, null, out var ka, da);
        orb.DetectAndCompute(gb, null, out var kb, db);
        if (ka.Length <= 10 || kb.Length <= 10 || da.Empty() || db.Empty()) return 0;
        using var bf = new BFMatcher(NormTypes.Hamming);
        var good = bf.KnnMatch(da, db, 2).Where(m => m.Length == 2 && m[0].Distance < 0.75 * m[1].Distance).Select(m => m[0]).ToList();
        if (good.Count < 8) return 0;
        var src = good.Select(g => ka[g.QueryIdx].Pt).Select(p => new Point2d(p.X, p.Y));
        var dst = good.Select(g => kb[g.TrainIdx].Pt).Select(p => new Point2d(p.X, p.Y));
        using var mask = new Mat();
        using var h = Cv2.FindHomography(src, dst, HomographyMethods.Ransac, 6.0, mask);
        return mask.Empty() ? 0 : Cv2.CountNonZero(mask);
    }

    private static Mat? Grey(byte[] bytes)
    {
        using var colour = Decode(bytes);
        if (colour is null || colour.Empty()) return null;
        var s = 600.0 / Math.Max(colour.Width, colour.Height);
        using var small = colour.Resize(new OpenCvSharp.Size((int)(colour.Width * s), (int)(colour.Height * s)));
        var grey = new Mat();
        Cv2.CvtColor(small, grey, ColorConversionCodes.BGR2GRAY);
        return grey;
    }

    // OpenCV reads jpeg, png and webp itself; anything else goes through ImageSharp to jpeg first.
    private static Mat? Decode(byte[] bytes)
    {
        var m = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (!m.Empty()) return m;
        m.Dispose();
        try
        {
            using var img = SixLabors.ImageSharp.Image.Load(bytes);
            using var ms = new MemoryStream();
            img.Save(ms, new JpegEncoder { Quality = 90 });
            return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }
}
