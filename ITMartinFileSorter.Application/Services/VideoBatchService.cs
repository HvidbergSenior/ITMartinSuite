using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class VideoBatchService : IVideoBatchService
{
    private readonly FastUniversalVideoConverterService _converter;

    public VideoBatchService(FastUniversalVideoConverterService converter)
    {
        _converter = converter;
    }

    public async Task ConvertAllVideosAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null)
    {
        var videos = files
            .Where(f => f.Type == MediaType.Video && !string.IsNullOrEmpty(f.ExportedPath))
            .ToList();

        int total = videos.Count;
        int current = 0;

        foreach (var file in videos)
        {
            try
            {
                var folder = Path.GetDirectoryName(file.ExportedPath!)!;

                var output = await _converter
                    .ConvertToUniversalMp4Async(file.ExportedPath!, folder);

                // 🔥 IMPORTANT
                file.ExportedPath = output;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VIDEO ERROR] {file.FileName}: {ex}");
                continue;
            }

            current++;

            progress?.Invoke(current, total, file.FileName);
        }
    }
}