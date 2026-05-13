using System.Text.RegularExpressions;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Infrastructure.Enums;
using ITMartin.OCR.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Magic.Infrastructure.OCR;

public class CardRecognitionService
    : ICardRecognitionService
{
    private readonly IOcrService _ocrService;

    public CardRecognitionService(
        IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<CardDetectionResult?> DetectAsync(
        string imagePath)
    {
        try
        {
            // =====================================
            // VALIDATE IMAGE
            // =====================================

            var blurScore =
                await CalculateBlurScoreAsync(
                    imagePath);

            Console.WriteLine(
                $"FINAL BLUR SCORE: {blurScore}");

            // =====================================
            // NORMALIZE
            // =====================================

            var normalizedPath =
                await NormalizeCardAsync(
                    imagePath);

            Console.WriteLine(
                $"NORMALIZED: {normalizedPath}");

            // =====================================
            // OCR CROPS
            // =====================================

            var titleCrop =
                await CropAsync(
                    normalizedPath,
                    CropType.Title);

            var bottomCrop =
                await CropAsync(
                    normalizedPath,
                    CropType.Bottom);

            var setCrop =
                await CropAsync(
                    normalizedPath,
                    CropType.SetSymbol);

            var ptCrop =
                await CropAsync(
                    normalizedPath,
                    CropType.PowerToughness);

            Console.WriteLine(
                $"TITLE CROP: {titleCrop}");

            Console.WriteLine(
                $"BOTTOM CROP: {bottomCrop}");

            Console.WriteLine(
                $"SET CROP: {setCrop}");

            Console.WriteLine(
                $"PT CROP: {ptCrop}");

            // =====================================
            // OCR
            // =====================================

            var titleText =
                await _ocrService
                    .ExtractTextAsync(titleCrop);

            var bottomText =
                await _ocrService
                    .ExtractTextAsync(bottomCrop);

            var setText =
                await _ocrService
                    .ExtractTextAsync(setCrop);

            var ptText =
                await _ocrService
                    .ExtractTextAsync(ptCrop);

            Console.WriteLine(
                $"TITLE OCR: {titleText}");

            Console.WriteLine(
                $"BOTTOM OCR: {bottomText}");

            Console.WriteLine(
                $"SET OCR: {setText}");

            Console.WriteLine(
                $"PT OCR: {ptText}");

            // =====================================
            // BORDER DETECTION
            // =====================================

            var oldBorder =
                IsOldBorder(normalizedPath);

            var whiteBorder =
                IsWhiteBorder(normalizedPath);

            Console.WriteLine(
                $"OLD BORDER DETECTED: {oldBorder}");

            Console.WriteLine(
                $"WHITE BORDER DETECTED: {whiteBorder}");

            // =====================================
            // RESULT
            // =====================================

            var result =
                new CardDetectionResult
                {
                    Name =
                        CleanupTitle(
                            ExtractFirstLine(
                                titleText)),

                    CollectorNumber =
                        ExtractCollectorNumber(
                            bottomText),

                    Artist =
                        ExtractArtist(
                            bottomText),

                    Copyright =
                        ExtractCopyright(
                            bottomText),

                    SetCode =
                        ExtractSetCode(
                            setText),

                    PowerToughness =
                        ExtractPowerToughness(
                            ptText),

                    IsOldBorder =
                        oldBorder,

                    IsWhiteBorder =
                        whiteBorder,

                    OcrConfidence =
                        (decimal)Math.Min(
                            blurScore / 20d,
                            1d)
                };

            Console.WriteLine(
                $"FINAL OCR RESULT: " +
                $"NAME={result.Name} | " +
                $"SET={result.SetCode} | " +
                $"COLLECTOR={result.CollectorNumber} | " +
                $"ARTIST={result.Artist} | " +
                $"PT={result.PowerToughness}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            return null;
        }
    }

    // =========================================
    // NORMALIZE CARD
    // =========================================

    private async Task<string> NormalizeCardAsync(
        string path)
    {
        using var image =
            await Image.LoadAsync(path);

        image.Mutate(x =>
        {
            x.AutoOrient();

            x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(1200, 1680)
            });
        });

        var output =
            Path.Combine(
                Path.GetDirectoryName(path)!,
                $"normalized_{Guid.NewGuid()}.jpg");

        await image.SaveAsJpegAsync(output);

        return output;
    }

    // =========================================
    // BLUR SCORE
    // =========================================

    private async Task<double> CalculateBlurScoreAsync(
        string path)
    {
        using var image =
            await Image.LoadAsync<Rgba32>(path);

        double total = 0;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width - 1; x++)
            {
                var p1 = image[x, y];
                var p2 = image[x + 1, y];

                var gray1 =
                    (p1.R + p1.G + p1.B) / 3d;

                var gray2 =
                    (p2.R + p2.G + p2.B) / 3d;

                total +=
                    Math.Abs(gray1 - gray2);
            }
        }

        return total /
               (image.Width * image.Height);
    }

    // =========================================
    // CROP
    // =========================================

    private async Task<string> CropAsync(
        string path,
        CropType type)
    {
        using var image =
            await Image.LoadAsync(path);

        Rectangle crop;

        switch (type)
        {
            case CropType.Title:

                crop = new Rectangle(
                    (int)(image.Width * 0.08),
                    (int)(image.Height * 0.04),
                    (int)(image.Width * 0.72),
                    (int)(image.Height * 0.06));

                break;

            case CropType.Bottom:

                crop = new Rectangle(
                    (int)(image.Width * 0.07),
                    (int)(image.Height * 0.84),
                    (int)(image.Width * 0.74),
                    (int)(image.Height * 0.06));

                break;

            case CropType.SetSymbol:

                crop = new Rectangle(
                    (int)(image.Width * 0.74),
                    (int)(image.Height * 0.55),
                    (int)(image.Width * 0.09),
                    (int)(image.Height * 0.07));

                break;

            case CropType.PowerToughness:

                crop = new Rectangle(
                    (int)(image.Width * 0.83),
                    (int)(image.Height * 0.84),
                    (int)(image.Width * 0.11),
                    (int)(image.Height * 0.06));

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(type),
                    type,
                    null);
        }

        Console.WriteLine(
            $"CROP {type}: " +
            $"X={crop.X} " +
            $"Y={crop.Y} " +
            $"W={crop.Width} " +
            $"H={crop.Height}");

        // =====================================
        // RAW DEBUG
        // =====================================

        using var rawDebug =
            image.Clone(x => x.Crop(crop));

        var rawPath =
            Path.Combine(
                Path.GetDirectoryName(path)!,
                $"{type}_RAW_{Guid.NewGuid()}.jpg");

        await rawDebug.SaveAsJpegAsync(rawPath);

        Console.WriteLine(
            $"RAW CROP: {rawPath}");

        // =====================================
        // OCR PROCESSING
        // =====================================

        image.Mutate(x =>
        {
            x.Crop(crop);

            x.Resize(
                crop.Width * 4,
                crop.Height * 4);

            x.AutoOrient();
        });

        var output =
            Path.Combine(
                Path.GetDirectoryName(path)!,
                $"{type}_{Guid.NewGuid()}.jpg");

        await image.SaveAsJpegAsync(output);

        Console.WriteLine(
            $"OCR CROP: {output}");

        return output;
    }

    // =========================================
    // CLEANUP TITLE
    // =========================================

    private static string? CleanupTitle(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text =
            text.Replace("|", "")
                .Replace("[", "")
                .Replace("]", "")
                .Trim();

        return text;
    }

    // =========================================
    // OCR HELPERS
    // =========================================

    private static string? ExtractFirstLine(
        string text)
    {
        return text
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();
    }

    private static string? ExtractCollectorNumber(
        string text)
    {
        var match =
            Regex.Match(
                text,
                @"(\d{1,3})");

        return match.Success
            ? match.Value
            : null;
    }

    private static string? ExtractArtist(
        string text)
    {
        var match =
            Regex.Match(
                text,
                @"Illus\.\s*(.+)",
                RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : null;
    }

    private static string? ExtractPowerToughness(
        string text)
    {
        var match =
            Regex.Match(
                text,
                @"(\d+)\s*\/\s*(\d+)");

        return match.Success
            ? $"{match.Groups[1].Value}/{match.Groups[2].Value}"
            : null;
    }

    private static string? ExtractCopyright(
        string text)
    {
        var match =
            Regex.Match(
                text,
                @"(19|20)\d{2}");

        return match.Success
            ? match.Value
            : null;
    }

    private static string? ExtractSetCode(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text =
            text
                .ToUpperInvariant()
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "");

        text =
            Regex.Replace(
                text,
                @"[^A-Z0-9]",
                "");

        if (text.Length > 5)
        {
            text =
                text[..5];
        }

        return text;
    }

    // =========================================
    // OLD BORDER DETECTION
    // =========================================

    private static bool IsOldBorder(
        string path)
    {
        try
        {
            using Image<Rgba32> image =
                Image.Load<Rgba32>(path);

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
                    image[point.X, point.Y];

                total +=
                    (pixel.R +
                     pixel.G +
                     pixel.B) / 3d;
            }

            var brightness =
                total / samplePoints.Length;

            Console.WriteLine(
                $"OLD BORDER BRIGHTNESS: {brightness}");

            return brightness < 110;
        }
        catch
        {
            return false;
        }
    }

    // =========================================
    // WHITE BORDER DETECTION
    // =========================================

    private static bool IsWhiteBorder(
        string path)
    {
        try
        {
            using Image<Rgba32> image =
                Image.Load<Rgba32>(path);

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
                    image[point.X, point.Y];

                total +=
                    (pixel.R +
                     pixel.G +
                     pixel.B) / 3d;
            }

            var brightness =
                total / samplePoints.Length;

            Console.WriteLine(
                $"WHITE BORDER BRIGHTNESS: {brightness}");

            return brightness > 180;
        }
        catch
        {
            return false;
        }
    }
}