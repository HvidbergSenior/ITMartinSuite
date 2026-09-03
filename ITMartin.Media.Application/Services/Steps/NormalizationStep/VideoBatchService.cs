using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class VideoBatchService : IVideoBatchService
{
    private static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromMinutes(1);

    // QuickSort is meant to stay fast - a full ffmpeg transcode of a large
    // file (a home movie transferred as-is, or a ripped film/TV episode
    // sitting in the source tree) doesn't belong in the same pass as sorting
    // thousands of small personal clips. Anything over this size, or under a
    // source folder that looks like commercial video, gets exported
    // untouched (LibraryExportService falls back to FullPath) and picked up
    // later by LargeVideoConvertService instead.
    private const long DeferSizeThresholdBytes = 150L * 1024 * 1024;

    private static readonly string[] DeferFolderNames =
        ["film", "movies", "movie", "tv", "series"];

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
        // QuickSort pipeline (~54 hours projected for 654 videos run one at a
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

            if (ShouldDefer(file))
            {
                file.IsDeferredLargeVideo = true;

                _logger.LogInformation(
                    "Deferring {File} ({SizeMb} MB) to LargeVideoConvert - QuickSort exports it as-is",
                    file.FileName,
                    file.SizeBytes / (1024 * 1024));

                progress?.Invoke(
                    itemNumber,
                    total,
                    file.FileName);

                return;
            }

            // Watchdog: a single hung ffmpeg process (corrupt/truncated source,
            // ffmpeg waiting on something that never comes) must not stall the
            // whole batch forever. Track the last time THIS file's progress
            // moved, and cancel just this file's token - not the outer batch
            // token - if it stalls for too long. Distinguishing "my own
            // watchdog fired" from "the whole batch/app is shutting down" is
            // what lets us skip the hung file and keep going instead of
            // rethrowing and killing every other in-flight conversion too.
            using var fileCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var lastProgressUtc = DateTime.UtcNow;
            using var watchdog = new Timer(
                _ =>
                {
                    if (DateTime.UtcNow - lastProgressUtc > StallTimeout)
                    {
                        fileCts.Cancel();
                    }
                },
                null,
                StallCheckInterval,
                StallCheckInterval);

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
                                lastProgressUtc = DateTime.UtcNow;
                                _logger.LogInformation(
                                    "Video progress {File}: {Progress:P0}",
                                    file.FileName,
                                    progressValue);
                            },
                            fileCts.Token,
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
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(
                    "Conversion for {File} appeared hung (no progress for {Timeout}) - killed and skipping, moving on to next file",
                    file.FileName,
                    StallTimeout);

                file.Failed = true;
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

    private static bool ShouldDefer(MediaFile file)
    {
        if (file.SizeBytes > DeferSizeThresholdBytes)
        {
            return true;
        }

        var segments = file.FullPath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return segments.Any(
            segment => DeferFolderNames.Contains(
                segment,
                StringComparer.OrdinalIgnoreCase));
    }
}