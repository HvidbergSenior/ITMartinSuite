using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Models;
using Tesseract;

namespace ITMartin.OCR.Services;

public sealed class OcrService
    : IOcrService
{
    public async Task<OcrResult?> ExtractTextAsync(
        OcrRegionResult regions)
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

                var title =
                    ReadRegion(
                        engine,
                        regions.TitleImagePath);

                var setCode =
                    ReadRegion(
                        engine,
                        regions.SetCodeImagePath);

                var artist =
                    ReadRegion(
                        engine,
                        regions.ArtistImagePath);

                var bottom =
                    ReadRegion(
                        engine,
                        regions.BottomInfoImagePath);

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
        string? path)
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

        using var page =
            engine.Process(image);

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