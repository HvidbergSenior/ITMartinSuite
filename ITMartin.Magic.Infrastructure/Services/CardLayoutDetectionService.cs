using ITMartin.Magic.Application.Interfaces;
using ITMartin.OCR.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class CardLayoutDetectionService
    : ICardLayoutDetectionService
{
    public CardLayoutType Detect(
        string normalizedCardPath)
    {
        try
        {
            using var image =
                Cv2.ImRead(
                    normalizedCardPath,
                    ImreadModes.Color);

            if (image.Empty())
            {
                return CardLayoutType.Unknown;
            }

            var sampleX =
                (int)(image.Width * 0.80);

            var sampleY =
                (int)(image.Height * 0.045);

            var pixel =
                image.At<Vec3b>(
                    sampleY,
                    sampleX);

            var brightness =
                (pixel.Item0 +
                 pixel.Item1 +
                 pixel.Item2) / 3d;

            return brightness > 120
                ? CardLayoutType.OldBorder
                : CardLayoutType.Modern;
        }
        catch
        {
            return CardLayoutType.Unknown;
        }
    }
}