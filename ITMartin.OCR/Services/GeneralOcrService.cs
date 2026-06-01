using Tesseract;
using ITMartin.OCR.Interfaces;

namespace ITMartin.OCR.Services;

public sealed class GeneralOcrService
    : IGeneralOcrService
{
    public async Task<string?> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(imagePath))
            {
                return null;
            }

            using var engine =
                new TesseractEngine(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "tessdata"),
                    "eng",
                    EngineMode.Default);

            using var image =
                Pix.LoadFromFile(imagePath);

            using var page =
                engine.Process(image);

            return page
                .GetText()
                ?.Trim();
        }, cancellationToken);
    }
}