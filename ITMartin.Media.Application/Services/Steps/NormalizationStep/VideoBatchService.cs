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

        // Full ffmpeg transcodes are the heaviest per-item work in the whole
        // Package1 pipeline (~54 hours projected for 654 videos run one at a
        // time), but unlike the lighter per-file steps this can't just use
        // Environment.ProcessorCount as the degree of parallelism: libx264
        // already spreads each single encode across multiple threads on its
        // own, so N fully-concurrent processes would each try to grab every
        // core and thrash instead of speeding anything up. Split the
        // machine's cores between a handful of concurrent processes instead,
        // and cap each process's own thread count via -threads so the total
        // stays near the real core count either way.
        var degreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount / 4));
        var ffmpegThreadsPerProcess = Math.Max(1, Environment.ProcessorCount / degreeOfParallelism);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(videos, parallelOptions, async (file, ct) =>
        {
            var itemNumber = Interlocked.Increment(ref current);

            progress?.Invoke(
                itemNumber - 1,
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
                            ct,
                            ffmpegThreadsPerProcess);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    file.NormalizedPath = output;
                    file.IsNormalized = true;
                    _logger.LogInformation(
                        "Video normalized {Source} -> {Output}",
                        file.FullPath,
                        output);
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
                itemNumber,
                total,
                file.FileName);
        });
    }
}