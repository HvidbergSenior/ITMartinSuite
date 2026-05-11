using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public sealed class MediaNormalizationService
    : IMediaNormalizationService
{
    private readonly IImageBatchService _imageService;
    private readonly IVideoBatchService _videoService;

    public MediaNormalizationService(
        IImageBatchService imageService,
        IVideoBatchService videoService)
    {
        _imageService = imageService;
        _videoService = videoService;
    }

    public async Task NormalizeAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null)
    {
        var images = files
            .Where(x => x.Type == MediaType.Image)
            .ToList();

        var videos = files
            .Where(x => x.Type == MediaType.Video)
            .ToList();

        // =========================
        // IMAGES
        // =========================

        await _imageService.ConvertAllImagesAsync(
            images,
            async (done, total, fileName) =>
            {
                var file = images.FirstOrDefault(x =>
                    x.FileName == fileName);

                if (file != null &&
                    !string.IsNullOrWhiteSpace(file.ExportedPath))
                {
                    file.NormalizedPath =
                        file.ExportedPath;
                }

                if (progress != null)
                {
                    await progress(
                        done,
                        total,
                        fileName);
                }
            });

        // =========================
        // VIDEOS
        // =========================

        await _videoService.ConvertAllVideosAsync(
            videos,
            async (done, total, fileName) =>
            {
                var file = videos.FirstOrDefault(x =>
                    x.FileName == fileName);

                if (file != null &&
                    !string.IsNullOrWhiteSpace(file.ExportedPath))
                {
                    file.NormalizedPath =
                        file.ExportedPath;
                }

                if (progress != null)
                {
                    await progress(
                        done,
                        total,
                        fileName);
                }
            });
    }
}