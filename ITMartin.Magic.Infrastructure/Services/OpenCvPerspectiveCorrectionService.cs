using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class OpenCvPerspectiveCorrectionService
    : IPerspectiveCorrectionService
{
    public async Task<string?> CorrectAsync(
        string imagePath,
        CardCornerDetectionResult corners, CancellationToken cancellationToken)
    {
        Console.WriteLine("CORRECTION SERVICE");

        Console.WriteLine(
            $"TL: {corners.TopLeft.X},{corners.TopLeft.Y}");
        Console.WriteLine(
            $"TL: {corners.TopLeft.X},{corners.TopLeft.Y}");

        Console.WriteLine(
            $"TR: {corners.TopRight.X},{corners.TopRight.Y}");

        Console.WriteLine(
            $"BR: {corners.BottomRight.X},{corners.BottomRight.Y}");

        Console.WriteLine(
            $"BL: {corners.BottomLeft.X},{corners.BottomLeft.Y}");
        return await Task.Run(() =>
        {
            var image = Cv2.ImRead(imagePath);

            Console.WriteLine(
                $"ORIGINAL IMAGE SIZE: {image.Width}x{image.Height}");
            if (image.Empty())
            {
                return null;
            }

            // =====================================
            // MTG CARD SIZE
            // =====================================

            const int outputWidth = 2400;

            const int outputHeight = 3360;

            // =====================================
            // SOURCE
            // =====================================

            var source =
                new[]
                {
                    new Point2f(
                        corners.TopLeft.X,
                        corners.TopLeft.Y),

                    new Point2f(
                        corners.TopRight.X,
                        corners.TopRight.Y),

                    new Point2f(
                        corners.BottomRight.X,
                        corners.BottomRight.Y),

                    new Point2f(
                        corners.BottomLeft.X,
                        corners.BottomLeft.Y)
                };

            // =====================================
            // DESTINATION
            // =====================================

            var destination =
                new[]
                {
                    new Point2f(0, 0),

                    new Point2f(
                        outputWidth - 1,
                        0),

                    new Point2f(
                        outputWidth - 1,
                        outputHeight - 1),

                    new Point2f(
                        0,
                        outputHeight - 1)
                };

            // =====================================
            // TRANSFORM
            // =====================================

            using var matrix =
                Cv2.GetPerspectiveTransform(
                    source,
                    destination);

            using var warped =
                new Mat();

            Cv2.WarpPerspective(
                image,
                warped,
                matrix,
                new OpenCvSharp.Size(
                    outputWidth,
                    outputHeight),

                InterpolationFlags.Lanczos4,

                BorderTypes.Replicate);

            // =====================================
            // SAVE
            // =====================================

            var folder =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "data",
                    "normalized");

            Directory.CreateDirectory(
                folder);

            var output =
                Path.Combine(
                    folder,
                    $"normalized_{Guid.NewGuid()}.jpg");

            Cv2.ImWrite(
                output,
                warped);

            Console.WriteLine(
                $"NORMALIZED: {output}");

            return output;
        });
    }
}