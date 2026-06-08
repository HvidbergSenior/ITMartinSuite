using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class VideoBatchService : IVideoBatchService
{
    private readonly IVideoConverterService
        _videoConverterService;

    private readonly ILogger<VideoBatchService>
        _logger;

    public VideoBatchService(
        IVideoConverterService videoConverterService,
        ILogger<VideoBatchService> logger)
    {
        _videoConverterService = videoConverterService;
        _logger = logger;
    }

    public async Task ConvertAllVideosAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null,
        CancellationToken cancellationToken = default)
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
            cancellationToken
                .ThrowIfCancellationRequested();

            current++;

            progress?.Invoke(
                current - 1,
                total,
                $"Converting {file.FileName}");

            try
            {
                _logger.LogInformation(
                    "Starting conversion for {File}",
                    file.FileName);

                var output =
                    await _videoConverterService
                        .ConvertToUniversalMp4Async(
                            file.NormalizedPath ??
                            file.FullPath,
                            tempRoot,
                            progressValue =>
                            {
                                _logger.LogInformation(
                                    "Video progress {File}: {Progress:P0}",
                                    file.FileName,
                                    progressValue);
                            },
                            cancellationToken);
                
                if (!string.IsNullOrWhiteSpace(output))
                {
                    file.NormalizedPath = output;

                    _logger.LogInformation(
                        "Conversion completed for {File}",
                        file.FileName);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Conversion cancelled for {File}",
                    file.FileName);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Video conversion failed for {File}",
                    file.FileName);

                file.Failed = true;
            }

            progress?.Invoke(
                current,
                total,
                file.FileName);
        }
    }
}