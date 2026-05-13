using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class OpenCvPerspectiveCorrectionService
    : IPerspectiveCorrectionService
{
    public async Task<string?> CorrectAsync(
        string imagePath,
        CardCornerDetectionResult corners)
    {
        return await Task.Run(() =>
        {
            using var image =
                Cv2.ImRead(imagePath);

            if (image.Empty())
            {
                return null;
            }

            // =====================================
            // MTG CARD SIZE
            // =====================================

            const int outputWidth = 1200;

            const int outputHeight = 1680;

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

                InterpolationFlags.Cubic,

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