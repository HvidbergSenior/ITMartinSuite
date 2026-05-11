using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public class VideoBatchService : IVideoBatchService
{
    private readonly VideoConverterService _converter;

    public VideoBatchService(VideoConverterService converter)
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