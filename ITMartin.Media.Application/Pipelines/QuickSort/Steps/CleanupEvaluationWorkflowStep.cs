using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Contracts.Entities;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class CleanupEvaluationWorkflowStep
    : QuickSortWorkflowStepBase
{
    private readonly QuickSortCleanupResultBuilder
        _cleanupResultBuilder;

    private readonly ILogger<
            CleanupEvaluationWorkflowStep>
        _logger;

    public CleanupEvaluationWorkflowStep(
        QuickSortCleanupResultBuilder cleanupResultBuilder,
        ILogger<CleanupEvaluationWorkflowStep> logger)
    {
        _cleanupResultBuilder =
            cleanupResultBuilder;

        _logger =
            logger;
    }

    public override string Name =>
        "Cleanup";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        cancellationToken
            .ThrowIfCancellationRequested();

        if (state.CleanupResult is not null)
        {
            _logger.LogInformation(
                "Cleanup already completed");

            return;
        }

        await ExecuteOperationAsync(
            "CleanupEvaluation",
            $"Files={state.MediaFiles.Count}",
            async () =>
            {
                var total =
                    state.MediaFiles.Count;

                var current = 0;

                foreach (var mediaFile in state.MediaFiles)
                {
                    current++;

                    LogStepProgress(
                        _logger,
                        Name,
                        current,
                        total,
                        mediaFile.FileName);

                    mediaFile.Status =
                        MediaFileStatus.ToKeep;

                    mediaFile.CleanupDecision =
                        CleanupDecision.Keep;
                }

                foreach (var group in state.DuplicateGroups)
                {
                    var keep =
                        group.Files
                            .OrderByDescending(x => x.SizeBytes)
                            .First();

                    keep.Status =
                        MediaFileStatus.ToKeep;

                    keep.CleanupDecision =
                        CleanupDecision.Keep;

                    foreach (var duplicate in group.Files
                                 .Where(x => x != keep))
                    {
                        duplicate.Status =
                            MediaFileStatus.ToDelete;

                        duplicate.CleanupDecision =
                            CleanupDecision.Delete;

                        duplicate.ExportSubFolder =
                            "Duplicates";
                    }
                }

                foreach (var mediaFile in state.MediaFiles
                             .Where(f => f.ExportSubFolder != "Duplicates"))
                {
                    if (IsDeleteCandidate(mediaFile))
                        mediaFile.ExportSubFolder = "DeleteCandidates";
                }

                var result =
                    _cleanupResultBuilder.Run(
                        state.MediaFiles);

                state.CleanupResult =
                    result;

                _logger.LogInformation(
                    """
                    Cleanup completed
                    Keep: {Keep}
                    Delete: {Delete}
                    """,
                    result.KeepCount,
                    result.DeleteCount);

                await Task.CompletedTask;
            },
            _logger);
    }

    private static bool IsDeleteCandidate(MediaFile file)
    {
        // Video too short to be meaningful
        if (file.Duration.HasValue && file.Duration.Value.TotalSeconds < 3)
            return true;

        // Tiny image — likely icon, thumbnail, or web asset
        if (file.Width.HasValue && file.Height.HasValue &&
            file.Width.Value < 150 && file.Height.Value < 150)
            return true;

        return false;
    }
}