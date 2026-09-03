using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

/// <summary>
/// Runs right after Export. MediaRulesWorkflowStep dispatches every video's
/// conversion the moment it's classified (step 4), so by the time Export
/// runs (step 15) some are already done and got exported pre-converted via
/// the usual NormalizedPath ?? FullPath fallback - nothing to do for those.
/// This step handles the rest: for every dispatched conversion, once it
/// finishes (which can be well after QuickSort itself reports Completed),
/// swap the converted file in at its final ExportedPath if Export ended up
/// using the original. Fire-and-forget on purpose - QuickSort's own
/// completion must not wait on conversions that can take hours.
/// </summary>
public sealed class VideoConvertFinalizeWorkflowStep(
    IConcurrentVideoDispatcher dispatcher,
    ILogger<VideoConvertFinalizeWorkflowStep> logger)
    : QuickSortWorkflowStepBase
{
    public override string Name => "VideoConvertFinalize";

    public override Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var pending = dispatcher.GetPending();

        logger.LogInformation(
            "VideoConvertFinalize: {Total} videos dispatched during this run - swapping each in once its conversion finishes",
            pending.Count);

        foreach (var (file, task) in pending)
        {
            _ = FinalizeOnceReadyAsync(file, task);
        }

        return Task.CompletedTask;
    }

    private async Task FinalizeOnceReadyAsync(MediaFile file, Task conversionTask)
    {
        try
        {
            await conversionTask;
        }
        catch
        {
            // Already logged by the dispatcher itself.
            return;
        }

        if (!file.IsNormalized || string.IsNullOrWhiteSpace(file.NormalizedPath))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(file.ExportedPath) || !File.Exists(file.ExportedPath))
        {
            return;
        }

        if (!File.Exists(file.NormalizedPath))
        {
            return;
        }

        if (string.Equals(Path.GetExtension(file.ExportedPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            // Export already picked up the converted file (it finished in
            // time) - nothing to swap.
            return;
        }

        try
        {
            File.Delete(file.ExportedPath);

            var targetPath = Path.ChangeExtension(file.ExportedPath, ".mp4");
            File.Move(file.NormalizedPath, targetPath, overwrite: true);
            file.ExportedPath = targetPath;

            logger.LogInformation(
                "Swapped in converted video for {File} at {Path}",
                file.FileName,
                targetPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to swap in converted video for {File}", file.FileName);
        }
    }
}
