using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public sealed class MediaNormalizationService
    : IMediaNormalizationService
{
    private readonly IImageConverterService _imageConverter;

    private readonly IVideoConverterService _videoConverter;

    public MediaNormalizationService(
        IImageConverterService imageConverter,
        IVideoConverterService videoConverter)
    {
        _imageConverter = imageConverter;
        _videoConverter = videoConverter;
    }

    public async Task NormalizeAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null)
    {
        int total = files.Count;

        int done = 0;

        foreach (var file in files)
        {
            try
            {
                string? normalized = null;

                // =========================
                // IMAGE
                // =========================

                if (file.Type == MediaType.Image)
                {
                    normalized =
                        await _imageConverter
                            .ConvertToJpgAsync(
                                file.FullPath);
                }

                // =========================
                // VIDEO
                // =========================

                else if (file.Type == MediaType.Video)
                {
                    normalized =
                        await _videoConverter
                            .ConvertToUniversalMp4Async(
                                file.FullPath,
                                Path.GetTempPath());
                }

                // =========================
                // STORE NORMALIZED PATH
                // =========================

                file.NormalizedPath =
                    normalized ??
                    file.FullPath;

                Console.WriteLine(
                    $"NORMALIZED: {file.NormalizedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"NORMALIZE ERROR FILE: {file.FullPath}");

                Console.WriteLine(ex);
                
                file.NormalizedPath =
                    file.FullPath;
            }

            done++;

            if (progress != null)
            {
                await progress(
                    done,
                    total,
                    file.FileName);
            }
        }
    }
}