using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class OpenCvCardCornerDetectionService
    : ICardCornerDetectionService
{
    private const int MaxResizeWidth = 1600;

    private const int MinimumContourArea = 1000;

    private const int CannyThreshold1 = 40;

    private const int CannyThreshold2 = 120;

    public Task<CardCornerDetectionResult?> DetectAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            Detect(imagePath));
    }

    private static CardCornerDetectionResult? Detect(
        string imagePath)
    {
        try
        {
            var debugFolder =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "data",
                    "debug");

            Directory.CreateDirectory(
                debugFolder);

            using var original =
                Cv2.ImRead(
                    imagePath,
                    ImreadModes.Color);

            if (original.Empty())
            {
                return null;
            }

            Console.WriteLine(
                $"Image size: {original.Width}x{original.Height}");

            var scale =
                original.Width > MaxResizeWidth
                    ? (double)MaxResizeWidth /
                      original.Width
                    : 1.0;

            using var resized =
                scale == 1.0
                    ? original.Clone()
                    : original.Resize(
                        new OpenCvSharp.Size(
                            (int)(original.Width * scale),
                            (int)(original.Height * scale)));

            using var gray =
                new Mat();

            Cv2.CvtColor(
                resized,
                gray,
                ColorConversionCodes.BGR2GRAY);

            using var enhanced =
                new Mat();

            Cv2.EqualizeHist(
                gray,
                enhanced);

            using var blurred =
                new Mat();

            Cv2.GaussianBlur(
                enhanced,
                blurred,
                new OpenCvSharp.Size(7, 7),
                0);

            using var edges =
                new Mat();

            Cv2.Canny(
                blurred,
                edges,
                CannyThreshold1,
                CannyThreshold2);

            using var kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new OpenCvSharp.Size(5, 5));

            Cv2.Dilate(
                edges,
                edges,
                kernel);

            Cv2.ImWrite(
                Path.Combine(
                    debugFolder,
                    "edges.jpg"),
                edges);

            Cv2.FindContours(
                edges,
                out var contours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            Console.WriteLine(
                $"Contours found: {contours.Length}");

            OpenCvSharp.Point[]? bestQuad = null;

            double bestArea = 0;

            foreach (var contour in contours)
            {
                var perimeter =
                    Cv2.ArcLength(
                        contour,
                        true);

                var approx =
                    Cv2.ApproxPolyDP(
                        contour,
                        0.03 * perimeter,
                        true);

                if (approx.Length < 4 ||
                    approx.Length > 10)
                {
                    continue;
                }

                if (!Cv2.IsContourConvex(
                        approx))
                {
                    continue;
                }

                var area =
                    Cv2.ContourArea(
                        approx);

                if (area < MinimumContourArea)
                {
                    continue;
                }

                var rect =
                    Cv2.BoundingRect(
                        contour);

                Console.WriteLine(
                    $"Area={area} Width={rect.Width} Height={rect.Height}");

                if (rect.Width < 80)
                {
                    continue;
                }

                if (rect.Height < 120)
                {
                    continue;
                }

                var ratio =
                    (double)rect.Width /
                    rect.Height;

                Console.WriteLine(
                    $"Ratio={ratio}");

                // Magic card portrait ratio
                if (ratio < 0.50 ||
                    ratio > 0.90)
                {
                    continue;
                }

                using var candidate =
                    resized.Clone();

                Cv2.Rectangle(
                    candidate,
                    rect,
                    Scalar.Lime,
                    4);

                Cv2.ImWrite(
                    Path.Combine(
                        debugFolder,
                        $"candidate_{area}_{Guid.NewGuid()}.jpg"),
                    candidate);

                if (area > bestArea)
                {
                    bestArea = area;

                    bestQuad =
                        new[]
                        {
                            new OpenCvSharp.Point(rect.Left, rect.Top),
                            new OpenCvSharp.Point(rect.Right, rect.Top),
                            new OpenCvSharp.Point(rect.Right, rect.Bottom),
                            new OpenCvSharp.Point(rect.Left, rect.Bottom)
                        };
                }
            }

            if (bestQuad is null)
            {
                Console.WriteLine(
                    "NO VALID CARD CONTOUR FOUND");

                return null;
            }

            using var debug =
                resized.Clone();

            Cv2.Polylines(
                debug,
                new[] { bestQuad },
                true,
                Scalar.Lime,
                6);

            Cv2.ImWrite(
                Path.Combine(
                    debugFolder,
                    $"quad_{Guid.NewGuid()}.jpg"),
                debug);

            var ordered =
                OrderPoints(
                    bestQuad);

            Console.WriteLine(
                $"Area: {bestArea}");

            var reverseScale =
                1.0 / scale;

            return new CardCornerDetectionResult
            {
                Success = true,

                TopLeft =
                    ScalePoint(
                        ordered[0],
                        reverseScale),

                TopRight =
                    ScalePoint(
                        ordered[1],
                        reverseScale),

                BottomRight =
                    ScalePoint(
                        ordered[2],
                        reverseScale),

                BottomLeft =
                    ScalePoint(
                        ordered[3],
                        reverseScale)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "CORNER DETECTION FAILED");

            Console.WriteLine(ex);

            throw;
        }
    }

    private static OpenCvSharp.Point[] OrderPoints(
        OpenCvSharp.Point[] points)
    {
        var ordered =
            new OpenCvSharp.Point[4];

        ordered[0] =
            points.OrderBy(
                p => p.X + p.Y).First();

        ordered[2] =
            points.OrderByDescending(
                p => p.X + p.Y).First();

        ordered[1] =
            points.OrderBy(
                p => p.Y - p.X).First();

        ordered[3] =
            points.OrderByDescending(
                p => p.Y - p.X).First();

        return ordered;
    }

    private static CardPoint ScalePoint(
        OpenCvSharp.Point point,
        double scale)
    {
        return new CardPoint
        {
            X = (float)(point.X * scale),
            Y = (float)(point.Y * scale)
        };
    }
}