using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

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
            try
            {
                var output =
                    await _converter.ConvertToJpgAsync(
                        file.FullPath);

                // IMPORTANT

                file.NormalizedPath = output;

                Console.WriteLine(
                    $"NORMALIZED IMAGE: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[IMAGE ERROR] {file.FileName}: {ex}");

                // fallback

                file.NormalizedPath =
                    file.FullPath;
            }

            current++;

            progress?.Invoke(
                current,
                total,
                file.FileName);
        }
    }
}