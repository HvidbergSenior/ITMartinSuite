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

                var title =
                    ReadRegion(
                        engine,
                        regions.TitleImagePath,
                        PageSegMode.SingleWord);

                var setCode =
                    ReadRegion(
                        engine,
                        regions.SetCodeImagePath,
                        PageSegMode.SingleWord);
                return new OcrResult
                {
                    Regions =
                    [
                        new OcrTextRegionResult
                        {
                            RegionName = "title",

                            Text =
                                Clean(title?.Text),

                            Confidence =
                                title?.Confidence ?? 0
                        },

                        new OcrTextRegionResult
                        {
                            RegionName = "set",

                            Text =
                                Clean(setCode?.Text),

                            Confidence =
                                setCode?.Confidence ?? 0
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

    private static OcrReadResult? ReadRegion(
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

        using var page =
            engine.Process(
                image,
                mode);

        return new OcrReadResult(
            page.GetText()?.Trim(),
            page.GetMeanConfidence());
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}