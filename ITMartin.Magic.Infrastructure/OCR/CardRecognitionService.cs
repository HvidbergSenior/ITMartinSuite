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
        var titleCrop =
            await CropAsync(
                imagePath,
                CropType.Title);

        var bottomCrop =
            await CropAsync(
                imagePath,
                CropType.Bottom);

        var setCrop =
            await CropAsync(
                imagePath,
                CropType.SetSymbol);

        var titleText =
            await _ocrService
                .ExtractTextAsync(titleCrop);

        var bottomText =
            await _ocrService
                .ExtractTextAsync(bottomCrop);

        var setText =
            await _ocrService
                .ExtractTextAsync(setCrop);

        Console.WriteLine(
            $"TITLE OCR: {titleText}");

        Console.WriteLine(
            $"BOTTOM OCR: {bottomText}");

        Console.WriteLine(
            $"SET OCR: {setText}");

        var oldBorder =
            IsOldBorder(imagePath);

        var whiteBorder =
            IsWhiteBorder(imagePath);

        Console.WriteLine(
            $"OLD BORDER DETECTED: {oldBorder}");

        Console.WriteLine(
            $"WHITE BORDER DETECTED: {whiteBorder}");

        var result =
            new CardDetectionResult
            {
                Name =
                    ExtractFirstLine(titleText),

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

                IsOldBorder =
                    oldBorder,

                IsWhiteBorder =
                    whiteBorder
            };

        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);

        return null;
    }
}

    private async Task<string> CropAsync(
        string path,
        CropType type)
    {
        using var image =
            await Image.LoadAsync(path);

        var width =
            image.Width;

        var height =
            image.Height;

        // =====================================
        // CARD POSITION
        // =====================================

        var cardX =
            width * 0.31;

        var cardY =
            height * 0.17;

        var cardWidth =
            width * 0.36;

        var cardHeight =
            height * 0.66;

        Rectangle crop;

        switch (type)
        {
            case CropType.Title:

                crop = new Rectangle(
                    (int)(image.Width * 0.25),
                    (int)(image.Height * 0.08),
                    (int)(image.Width * 0.56),
                    (int)(image.Height * 0.07));
                break;

            case CropType.Bottom:

                crop = new Rectangle(
                    (int)(image.Width * 0.28),
                    (int)(image.Height * 0.76),
                    (int)(image.Width * 0.44),
                    (int)(image.Height * 0.08));
                break;

            case CropType.SetSymbol:

                crop = new Rectangle(
                    (int)(image.Width * 0.71),
                    (int)(image.Height * 0.735),
                    (int)(image.Width * 0.09),
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

        image.Mutate(x =>
        {
            x.Crop(crop);

            x.Resize(
                crop.Width * 8,
                crop.Height * 8);

            x.AutoOrient();

            x.Grayscale();

            x.Contrast(5f);

            x.Brightness(1.25f);

            x.GaussianSharpen(4f);
        });

        var output =
            Path.Combine(
                Path.GetDirectoryName(path)!,
                $"{type}_{Guid.NewGuid()}.jpg");

        await image.SaveAsJpegAsync(output);

        return output;
    }

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
    private static bool IsOldBorder(
        string path)
    {
        try
        {
            using Image<Rgba32> image =
                Image.Load<Rgba32>(path);

            // SAMPLE OLD FRAME RED AREA

            var x =
                (int)(image.Width * 0.42);

            var y =
                (int)(image.Height * 0.22);

            var pixel =
                image[x, y];

            Console.WriteLine(
                $"OLD BORDER SAMPLE: " +
                $"R={pixel.R} " +
                $"G={pixel.G} " +
                $"B={pixel.B}");

            // old cards are darker/desaturated

            return pixel.R < 170;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWhiteBorder(
        string path)
    {
        try
        {
            using Image<Rgba32> image =
                Image.Load<Rgba32>(path);

            // SAMPLE LEFT BORDER

            var x =
                (int)(image.Width * 0.34);

            var y =
                (int)(image.Height * 0.55);

            var pixel =
                image[x, y];

            var brightness =
                (pixel.R + pixel.G + pixel.B) / 3;

            Console.WriteLine(
                $"WHITE BORDER SAMPLE: " +
                $"R={pixel.R} " +
                $"G={pixel.G} " +
                $"B={pixel.B} " +
                $"BR={brightness}");

            return brightness > 170;
        }
        catch
        {
            return false;
        }
    }
}