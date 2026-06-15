using ITMartin.Magic.Application.Interfaces;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class BorderClassificationService
    : IBorderClassificationService
{
    private readonly ILogger<BorderClassificationService> _logger;

    public BorderClassificationService(ILogger<BorderClassificationService> logger)
    {
        _logger = logger;
    }
    public bool IsOldBorder(
        string imagePath)
    {
        try
        {
            using var image =
                Cv2.ImRead(imagePath);

            if (image.Empty())
            {
                return false;
            }

            var samplePoints =
                new[]
                {
                    new Point(
                        (int)(image.Width * 0.10),
                        (int)(image.Height * 0.10)),

                    new Point(
                        (int)(image.Width * 0.90),
                        (int)(image.Height * 0.10))
                };

            double total = 0;

            foreach (var point in samplePoints)
            {
                var pixel =
                    image.At<Vec3b>(
                        point.Y,
                        point.X);

                total +=
                    (pixel.Item0 +
                     pixel.Item1 +
                     pixel.Item2) / 3d;
            }

            var brightness =
                total / samplePoints.Length;

            _logger.LogDebug("Old border brightness: {Brightness}", brightness);

            return brightness < 165;
        }
        catch
        {
            return false;
        }
    }

    public bool IsWhiteBorder(
        string imagePath)
    {
        try
        {
            using var image =
                Cv2.ImRead(imagePath);

            if (image.Empty())
            {
                return false;
            }

            var samplePoints =
                new[]
                {
                    new Point(
                        (int)(image.Width * 0.02),
                        (int)(image.Height * 0.50)),

                    new Point(
                        (int)(image.Width * 0.98),
                        (int)(image.Height * 0.50))
                };

            double total = 0;

            foreach (var point in samplePoints)
            {
                var pixel =
                    image.At<Vec3b>(
                        point.Y,
                        point.X);

                total +=
                    (pixel.Item0 +
                     pixel.Item1 +
                     pixel.Item2) / 3d;
            }

            var brightness =
                total / samplePoints.Length;

            _logger.LogDebug("White border brightness: {Brightness}", brightness);

            return brightness > 125;
        }
        catch
        {
            return false;
        }
    }
}