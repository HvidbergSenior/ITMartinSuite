using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain;
using ITMartin.Magic.Infrastructure.Enums;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class CardLayoutDetectionService
    : ICardLayoutDetectionService
{
    public CardLayoutType Detect(
        string normalizedCardPath)
    {
        try
        {
            using var image =
                Cv2.ImRead(normalizedCardPath);

            if (image.Empty())
            {
                return CardLayoutType.Unknown;
            }

            // =====================================
            // SAMPLE TITLE BAR
            // =====================================

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

            Console.WriteLine(
                $"LAYOUT BRIGHTNESS: {brightness}");

            // =====================================
            // OLD BORDER:
            // brighter faded title bars
            // =====================================

            if (brightness > 120)
            {
                Console.WriteLine(
                    "LAYOUT: OLD BORDER");

                return CardLayoutType.OldBorder;
            }

            Console.WriteLine(
                "LAYOUT: MODERN");

            return CardLayoutType.Modern;
        }
        catch
        {
            return CardLayoutType.Unknown;
        }
    }
}