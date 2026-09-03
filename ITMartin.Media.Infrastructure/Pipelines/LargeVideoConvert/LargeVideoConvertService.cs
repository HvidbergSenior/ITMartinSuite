using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.LargeVideoConvert;

/// <summary>
/// Follow-up pass for whatever QuickSort deferred (see
/// MediaFile.IsDeferredLargeVideo / VideoBatchService.ShouldDefer). Reuses
/// the same IVideoConverterService and the same per-file stall watchdog
/// approach as QuickSort's VideoBatchService, but processes files one at a
/// time - these are large files by definition, and this is meant to run
/// unattended on whatever machine has spare capacity, not compete with
/// itself for disk/CPU the way QuickSort's small-file batch does.
/// </summary>
public sealed class LargeVideoConvertService(
    IVideoConverterService videoConverterService,
    IWorkflowInstanceStore workflowInstanceStore,
    IWorkflowAlertNotifier workflowAlertNotifier,
    ILogger<LargeVideoConvertService> logger)
    : ILargeVideoConvertService
{
    private static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromMinutes(1);

    // Constructed directly rather than DI-injected - QuickSortManifestLoader is
    // only registered via AddQuickSortPipeline (the Worker's DI graph), not
    // FileSorter.Server's, and it has a parameterless constructor anyway. Same
    // reasoning as ImageTaggingService's identical field.
    private readonly QuickSortManifestLoader _manifestLoader = new();

    public async Task<LargeVideoConvertResult> ConvertDeferredVideosAsync(
        string libraryPath,
        Action<int, int, string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.NewGuid();
        await workflowInstanceStore.CreateAsync(workflowId, "LargeVideoConvertWorkflow", cancellationToken);
        await workflowInstanceStore.SetRunningStepAsync(workflowId, "ConvertDeferredVideos", cancellationToken);

        try
        {
            // Throws FileNotFoundException if manifest.json isn't there yet -
            // that's the precondition check: QuickSort has to have run first.
            var manifest = await _manifestLoader.LoadAsync(libraryPath, cancellationToken);

            var deferred = manifest.MediaFiles
                .Where(f => f.IsDeferredLargeVideo)
                .ToList();

            var total = deferred.Count;
            var converted = 0;
            var failed = 0;

            for (var i = 0; i < deferred.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = deferred[i];

                progress?.Invoke(i, total, file.FileName);
                await workflowInstanceStore.SetProgressAsync(workflowId, i, total, file.FileName, cancellationToken: cancellationToken);

                if (await ConvertOneAsync(file, cancellationToken))
                {
                    converted++;
                }
                else
                {
                    failed++;
                }
            }

            progress?.Invoke(total, total, "Done");
            await workflowInstanceStore.SetProgressAsync(workflowId, total, total, "Done", cancellationToken: cancellationToken);
            await workflowInstanceStore.MarkCompletedAsync(workflowId, cancellationToken);

            var result = new LargeVideoConvertResult
            {
                TotalDeferred = total,
                Converted = converted,
                Failed = failed
            };

            await workflowAlertNotifier.NotifyCompletedAsync(
                workflowId,
                "LargeVideoConvertWorkflow",
                TimeSpan.Zero,
                CancellationToken.None);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await workflowInstanceStore.MarkFailedAsync(workflowId, ex.Message, CancellationToken.None);
            await workflowAlertNotifier.NotifyFailedAsync(workflowId, "LargeVideoConvertWorkflow", ex.Message, CancellationToken.None);
            throw;
        }
    }

    private async Task<bool> ConvertOneAsync(MediaFile file, CancellationToken outerCt)
    {
        var currentPath = file.ExportedPath ?? file.FullPath;
        var alreadyConvertedPath = Path.ChangeExtension(currentPath, ".mp4");

        if (!File.Exists(currentPath))
        {
            if (File.Exists(alreadyConvertedPath))
            {
                logger.LogInformation("{File} already converted (found {Mp4}), skipping", file.FileName, alreadyConvertedPath);
                return true;
            }

            logger.LogWarning("Deferred file {File} not found at {Path} - may have moved since QuickSort ran", file.FileName, currentPath);
            return false;
        }

        if (string.Equals(Path.GetExtension(currentPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("{File} is already .mp4, skipping", file.FileName);
            return true;
        }

        using var fileCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
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

        var outputDirectory = Path.GetDirectoryName(currentPath)!;

        try
        {
            logger.LogInformation("Converting deferred video {File}", file.FileName);

            var convertedPath = await videoConverterService.ConvertToUniversalMp4Async(
                currentPath,
                outputDirectory,
                _ => lastProgressUtc = DateTime.UtcNow,
                fileCts.Token);

            File.Delete(currentPath);
            logger.LogInformation("Converted {File} -> {Output}, removed original", file.FileName, convertedPath);
            return true;
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
        {
            logger.LogError("Conversion for {File} appeared hung (no progress for {Timeout}) - skipping", file.FileName, StallTimeout);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Large video conversion failed for {File}", file.FileName);
            return false;
        }
    }
}
