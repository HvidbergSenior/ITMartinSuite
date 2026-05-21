using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class OpenCvCardCornerDetectionService
    : ICardCornerDetectionService
{
    private const int MaxResizeWidth = 1600;

    private const int MinimumContourArea = 50000;

    private const int CannyThreshold1 = 40;

    private const int CannyThreshold2 = 120;

    public Task<CardCornerDetectionResult?> DetectAsync(
        string imagePath)
    {
        var result =
            Detect(imagePath);

        return Task.FromResult(result);
    }

    private static CardCornerDetectionResult? Detect(
        string imagePath)
    {
        try
        {
            using var original =
                Cv2.ImRead(
                    imagePath,
                    ImreadModes.Color);

            if (original.Empty())
            {
                return null;
            }

            var scale =
                (double)MaxResizeWidth /
                original.Width;

            var resizedHeight =
                (int)(original.Height * scale);

            using var resized =
                original.Resize(
                    new OpenCvSharp.Size(
                        MaxResizeWidth,
                        resizedHeight));

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

            Cv2.FindContours(
                edges,
                out var contours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

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

                if (!Cv2.IsContourConvex(approx))
                {
                    continue;
                }

                var area =
                    Cv2.ContourArea(approx);

                if (area < MinimumContourArea)
                {
                    continue;
                }

                if (area > bestArea)
                {
                    bestArea = area;
                    bestQuad = approx;
                }
            }

            if (bestQuad is null)
            {
                return null;
            }

            var ordered =
                OrderPoints(bestQuad);

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
        catch
        {
            return null;
        }
    }

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