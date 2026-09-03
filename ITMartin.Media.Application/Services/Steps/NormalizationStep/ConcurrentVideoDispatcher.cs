using System.Collections.Concurrent;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public sealed class ConcurrentVideoDispatcher(
    IVideoConverterService videoConverterService,
    ILogger<ConcurrentVideoDispatcher> logger)
    : IConcurrentVideoDispatcher
{
    private static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromMinutes(1);

    // Same reasoning as the batch converter this replaces: libx264 already
    // spreads one encode across multiple threads on its own, so bound how
    // many run at once instead of every dispatched file grabbing every core.
    private static readonly int DegreeOfParallelism =
        Math.Max(1, Math.Min(4, Environment.ProcessorCount / 4));

    private static readonly int FfmpegThreadsPerProcess =
        Math.Max(1, Environment.ProcessorCount / DegreeOfParallelism);

    // Deliberately not IDisposable - this is scoped per workflow run and the
    // owning DI scope disposes shortly after QuickSort's last step returns,
    // while conversions dispatched late in the run can still be in flight for
    // hours afterward. Disposing the semaphore out from under a still-queued
    // WaitAsync would throw ObjectDisposedException in a background task
    // nothing is watching. One leaked SemaphoreSlim handle per run is the
    // acceptable tradeoff.
    private readonly SemaphoreSlim _semaphore = new(DegreeOfParallelism);

    private readonly ConcurrentDictionary<Guid, (MediaFile File, Task ConversionTask)> _pending = new();

    public void Dispatch(MediaFile file, CancellationToken cancellationToken)
    {
        var task = Task.Run(() => ConvertOneAsync(file, cancellationToken), CancellationToken.None);
        _pending[file.Id] = (file, task);
    }

    public IReadOnlyList<(MediaFile File, Task ConversionTask)> GetPending() =>
        _pending.Values.ToList();

    private async Task ConvertOneAsync(MediaFile file, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "ITMartinFileSorter");
            Directory.CreateDirectory(tempRoot);

            // Same per-file hang watchdog as before: a corrupt/truncated
            // source can't be allowed to hold a concurrency slot forever.
            using var fileCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                logger.LogInformation("Starting concurrent conversion for {File}", file.FileName);

                var output = await videoConverterService.ConvertToUniversalMp4Async(
                    file.NormalizedPath ?? file.FullPath,
                    tempRoot,
                    _ => lastProgressUtc = DateTime.UtcNow,
                    fileCts.Token,
                    FfmpegThreadsPerProcess);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    file.NormalizedPath = output;
                    file.IsNormalized = true;
                    logger.LogInformation("Converted {File} -> {Output}", file.FileName, output);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    "Conversion for {File} appeared hung (no progress for {Timeout}) - abandoning, original will be exported as-is",
                    file.FileName,
                    StallTimeout);

                file.Failed = true;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Conversion cancelled for {File}", file.FileName);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Video conversion failed for {File}", file.FileName);
                file.Failed = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
