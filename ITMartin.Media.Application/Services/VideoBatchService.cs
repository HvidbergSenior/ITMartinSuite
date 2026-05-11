using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public class VideoBatchService : IVideoBatchService
{
    private readonly VideoConverterService _converter;

    public VideoBatchService(
        VideoConverterService converter)
    {
        _converter = converter;
    }

    public async Task ConvertAllVideosAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null)
    {
        var videos = files
            .Where(f => f.Type == MediaType.Video)
            .ToList();

        int total = videos.Count;
        int current = 0;

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ITMartinFileSorter");

        Directory.CreateDirectory(tempRoot);

        foreach (var file in videos)
        {
            try
            {
                var output =
                    await _converter
                        .ConvertToUniversalMp4Async(
                            file.FullPath,
                            tempRoot);

                // IMPORTANT

                file.NormalizedPath = output;

                Console.WriteLine(
                    $"NORMALIZED VIDEO: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[VIDEO ERROR] {file.FileName}: {ex}");

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