using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class OpenCvCardBoundaryDetectionService
    : ICardBoundaryDetectionService
{
    public async Task<CardCornerDetectionResult?> DetectAsync(
        string imagePath)
    {
        return await Task.Run(() =>
        {
            // =====================================
            // LOAD
            // =====================================

            using var original =
                Cv2.ImRead(imagePath);

            if (original.Empty())
            {
                return null;
            }

            // =====================================
            // RESIZE
            // =====================================

            const int maxWidth = 1600;

            var scale =
                (double)maxWidth /
                original.Width;

            var resizedHeight =
                (int)(original.Height * scale);

            using var resized =
                original.Resize(
                    new OpenCvSharp.Size(
                        maxWidth,
                        resizedHeight));

            // =====================================
            // GRAYSCALE
            // =====================================

            using var gray =
                new Mat();

            Cv2.CvtColor(
                resized,
                gray,
                ColorConversionCodes.BGR2GRAY);

            // =====================================
            // BLUR
            // =====================================

            using var blurred =
                new Mat();

            Cv2.GaussianBlur(
                gray,
                blurred,
                new OpenCvSharp.Size(5, 5),
                0);

            // =====================================
            // EDGE DETECTION
            // =====================================

            using var edges =
                new Mat();

            Cv2.Canny(
                blurred,
                edges,
                75,
                200);

            // =====================================
            // FIND CONTOURS
            // =====================================

            Cv2.FindContours(
                edges,
                out var contours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            // =====================================
            // FIND BEST RECTANGLE
            // =====================================

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
                        0.02 * perimeter,
                        true);

                if (approx.Length != 4)
                {
                    continue;
                }

                var area =
                    Cv2.ContourArea(approx);

                if (area < 100000)
                {
                    continue;
                }

                if (area > bestArea)
                {
                    bestArea = area;
                    bestQuad = approx;
                }
            }

            if (bestQuad == null)
            {
                return null;
            }

            // =====================================
            // ORDER POINTS
            // =====================================

            var ordered =
                OrderPoints(bestQuad);

            // =====================================
            // DEBUG IMAGE
            // =====================================

            using var debug =
                resized.Clone();

            for (var i = 0; i < 4; i++)
            {
                Cv2.Line(
                    debug,
                    ordered[i],
                    ordered[(i + 1) % 4],
                    Scalar.Lime,
                    8);
            }

            var debugFolder =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "data",
                    "debug");

            Directory.CreateDirectory(
                debugFolder);

            var debugPath =
                Path.Combine(
                    debugFolder,
                    $"{Guid.NewGuid()}.jpg");

            Cv2.ImWrite(
                debugPath,
                debug);

            // =====================================
            // SCALE BACK TO ORIGINAL
            // =====================================

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
                        reverseScale),

                DebugImagePath = debugPath
            };
        });
    }

    // =========================================
    // ORDER POINTS
    // =========================================

    private static OpenCvSharp.Point[] OrderPoints(
        OpenCvSharp.Point[] points)
    {
        var ordered =
            new OpenCvSharp.Point[4];

        ordered[0] =
            points.OrderBy(p => p.X + p.Y).First();

        ordered[2] =
            points.OrderByDescending(p => p.X + p.Y).First();

        ordered[1] =
            points.OrderBy(p => p.Y - p.X).First();

        ordered[3] =
            points.OrderByDescending(p => p.Y - p.X).First();

        return ordered;
    }

    // =========================================
    // SCALE
    // =========================================

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