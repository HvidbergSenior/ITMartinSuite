using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class ImageBatchService : IImageBatchService
{
    private readonly ImageConverterService _converter;

    public ImageBatchService(
        ImageConverterService converter)
    {
        _converter = converter;
    }

    public async Task ConvertAllImagesAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null)
    {
        var images = files
            .Where(f => f.Type == MediaType.Image)
            .ToList();

        int total = images.Count;
        int current = 0;

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ITMartinFileSorter");

        Directory.CreateDirectory(tempRoot);

        foreach (var file in images)
        {
            current++;

            progress?.Invoke(
                current - 1,
                total,
                $"Converting {file.FileName}");

            await Task.Yield();

            try
            {
                var output =
                    await _converter.ConvertToJpgAsync(
                        file.NormalizedPath ??
                        file.FullPath);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    file.NormalizedPath = output;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[IMAGE ERROR] {file.FileName}: {ex}");
            }

            progress?.Invoke(
                current,
                total,
                file.FileName);
        }
    }
}