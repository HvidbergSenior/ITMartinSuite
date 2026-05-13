using System.Text.RegularExpressions;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Interfaces;

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
        OcrRegionResult regions)
    {
        try
        {
            // =====================================
            // OCR
            // =====================================

            var titleText =
                await _ocrService
                    .ExtractTextAsync(
                        regions.TitleImagePath!);

            var bottomText =
                await _ocrService
                    .ExtractTextAsync(
                        regions.BottomInfoImagePath!);

            var setText =
                await _ocrService
                    .ExtractTextAsync(
                        regions.SetCodeImagePath!);

            var artistText =
                regions.ArtistImagePath != null
                    ? await _ocrService
                        .ExtractTextAsync(
                            regions.ArtistImagePath)
                    : "";

            Console.WriteLine(
                $"TITLE OCR: {titleText}");

            Console.WriteLine(
                $"BOTTOM OCR: {bottomText}");

            Console.WriteLine(
                $"SET OCR: {setText}");

            Console.WriteLine(
                $"ARTIST OCR: {artistText}");

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
                            $"{bottomText}\n{artistText}"),

                    Copyright =
                        ExtractCopyright(
                            bottomText),

                    SetCode =
                        ExtractSetCode(
                            setText),

                    PowerToughness =
                        ExtractPowerToughness(
                            bottomText),

                    OcrConfidence = 0.95m,

                    OcrDebugText =
                        $"""
                         TITLE:
                         {titleText}

                         BOTTOM:
                         {bottomText}

                         SET:
                         {setText}

                         ARTIST:
                         {artistText}
                         """
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
                .Replace("{", "")
                .Replace("}", "")
                .Replace("(", "")
                .Replace(")", "")
                .Trim();

        text =
            Regex.Replace(
                text,
                @"\s+",
                " ");

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

        return SetCodeDictionary.Normalize(
            text);
    }
}