using ITMartin.Media.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public class ImageBatchService : IImageBatchService
{
    private readonly ImageConverterService _converter;

    public ImageBatchService(ImageConverterService converter)
    {
        _converter = converter;
    }

    public async Task ConvertAllImagesAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null)
    {
        var images = files
            .Where(f => f.Type == MediaType.Image && !string.IsNullOrEmpty(f.ExportedPath))
            .ToList();

        int total = images.Count;
        int current = 0;

        foreach (var file in images)
        {
            try
            {
                var output = await _converter
                    .ConvertToJpgAsync(file.ExportedPath!);

                // 🔥 IMPORTANT
                file.ExportedPath = output;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IMAGE ERROR] {file.FileName}: {ex}");
            }

            current++;

            progress?.Invoke(current, total, file.FileName);
        }
    }
}