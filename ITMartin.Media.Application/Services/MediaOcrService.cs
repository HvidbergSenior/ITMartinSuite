using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Media.Application.Services;

public class MediaOcrService
    : IMediaOcrService
{
    private readonly
        IOcrRegionExtractor
        _ocrRegionExtractor;

    private readonly
        IOcrService
        _ocrService;

    public MediaOcrService(
        IOcrRegionExtractor ocrRegionExtractor,
        IOcrService ocrService)
    {
        _ocrRegionExtractor =
            ocrRegionExtractor;

        _ocrService =
            ocrService;
    }

    public async Task ProcessAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null)
    {
        var ocrFiles =
            files
                .Where(ShouldRunOcr)
                .ToList();

        int total =
            ocrFiles.Count;

        int done =
            0;

        foreach (var file in ocrFiles)
        {
            var path =
                file.NormalizedPath ??
                file.FullPath;

            try
            {
                Console.WriteLine(
                    $"OCR PATH USED: {path}");

                var regions =
                    await _ocrRegionExtractor
                        .ExtractAsync(
                            path);

                if (regions is null)
                {
                    Console.WriteLine(
                        $"OCR REGION EXTRACTION FAILED: {path}");

                    continue;
                }

                var result =
                    await _ocrService
                        .ExtractTextAsync(
                            regions);

                file.OcrText =
                    result is null
                        ? null
                        : string.Join(
                            Environment.NewLine,
                            result.Regions
                                .Select(x => x.Text));
                
                file.OcrProcessed =
                    true;

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