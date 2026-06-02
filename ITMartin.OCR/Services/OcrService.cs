using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Models;
using Tesseract;

namespace ITMartin.OCR.Services;

public sealed class OcrService
    : IOcrService
{
    public async Task<OcrResult?> ExtractTextAsync(
        OcrRegionResult regions, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var engine =
                    new TesseractEngine(
                        Path.Combine(
                            AppContext.BaseDirectory,
                            "tessdata"),
                        "eng",
                        EngineMode.Default);
                Console.WriteLine(
                    $"TITLE FILE: {regions.TitleImagePath}");

                Console.WriteLine(
                    $"SET FILE: {regions.SetCodeImagePath}");

                Console.WriteLine(
                    $"ARTIST FILE: {regions.ArtistImagePath}");

                Console.WriteLine(
                    $"BOTTOM FILE: {regions.BottomInfoImagePath}");
                var title =
                    ReadRegion(
                        engine,
                        regions.TitleImagePath,
                        PageSegMode.SingleLine);

                var artist =
                    ReadRegion(
                        engine,
                        regions.ArtistImagePath,
                        PageSegMode.SingleLine);

                var bottom =
                    ReadRegion(
                        engine,
                        regions.BottomInfoImagePath,
                        PageSegMode.Auto);

                var setCode =
                    ReadRegion(
                        engine,
                        regions.SetCodeImagePath,
                        PageSegMode.SingleWord);
                Console.WriteLine(
                    $"OCR TITLE: [{title}]");

                Console.WriteLine(
                    $"OCR SET: [{setCode}]");

                Console.WriteLine(
                    $"OCR ARTIST: [{artist}]");

                Console.WriteLine(
                    $"OCR BOTTOM: [{bottom}]");

                return new OcrResult
                {
                    Regions =
                    [
                        new OcrTextRegionResult
                        {
                            RegionName =
                                "title",

                            Text =
                                Clean(title),

                            Confidence =
                                1.0
                        },

                        new OcrTextRegionResult
                        {
                            RegionName =
                                "set",

                            Text =
                                Clean(setCode),

                            Confidence =
                                1.0
                        },

                        new OcrTextRegionResult
                        {
                            RegionName =
                                "artist",

                            Text =
                                Clean(artist),

                            Confidence =
                                1.0
                        },

                        new OcrTextRegionResult
                        {
                            RegionName =
                                "bottom",

                            Text =
                                Clean(bottom),

                            Confidence =
                                1.0
                        }
                    ]
                };
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    exception);

                return null;
            }
        });
    }

    private static string? ReadRegion(
        TesseractEngine engine,
        string? path,
        PageSegMode mode)
    {
        
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        using var image =
            Pix.LoadFromFile(path);

        Console.WriteLine(
            $"OCR IMAGE: {path}");

        Console.WriteLine(
            $"WIDTH: {image.Width}");

        Console.WriteLine(
            $"HEIGHT: {image.Height}");
        engine.SetVariable(
            "tessedit_char_whitelist",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-'");
        using var page =
            engine.Process(
                image,
                mode);
        Console.WriteLine(
            $"OCR CONFIDENCE: {page.GetMeanConfidence()}");
        return page
            .GetText();
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}