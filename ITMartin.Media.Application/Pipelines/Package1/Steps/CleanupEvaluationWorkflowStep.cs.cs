using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Contracts.Entities;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class CleanupEvaluationWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly Package1CleanupPipeline
        _cleanupPipeline;

    private readonly ILogger<
            CleanupEvaluationWorkflowStep>
        _logger;

    public CleanupEvaluationWorkflowStep(
        Package1CleanupPipeline cleanupPipeline,
        ILogger<CleanupEvaluationWorkflowStep> logger)
    {
        _cleanupPipeline =
            cleanupPipeline;

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
            context.State as Package1WorkflowState
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

                var result =
                    _cleanupPipeline.Run(
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
}