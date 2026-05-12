using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Media.Application.Services;

public class MediaOcrService : IMediaOcrService
{
    private readonly IOcrService _ocrService;

    public MediaOcrService(
        IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task ProcessAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null)
    {
        var ocrFiles =
            files
                .Where(ShouldRunOcr)
                .ToList();

        int total = ocrFiles.Count;

        int done = 0;

        foreach (var file in ocrFiles)
        {
            var path =
                file.NormalizedPath ??
                file.FullPath;

            try
            {
                Console.WriteLine(
                    $"OCR PATH USED: {path}");

                var text =
                    await _ocrService
                        .ExtractTextAsync(path);

                file.OcrText = text;

                file.OcrProcessed = true;

                Console.WriteLine(
                    $"OCR DONE: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"OCR ERROR: {ex}");
            }

            done++;

            if (progress != null)
            {
                await progress(
                    done,
                    total,
                    Path.GetFileName(path));
            }
        }
    }

    private static bool ShouldRunOcr(
        MediaFile file)
    {
        var path =
            file.NormalizedPath ??
            file.FullPath;

        var ext =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ext is
            ".jpg" or
            ".jpeg" or
            ".png";
    }
}