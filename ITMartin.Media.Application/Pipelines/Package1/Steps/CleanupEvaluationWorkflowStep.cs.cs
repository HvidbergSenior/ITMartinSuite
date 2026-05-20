using ITMartin.Media.Application.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Contracts.Entities;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class CleanupEvaluationWorkflowStep
    : IWorkflowStep
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

    public string Name => "Cleanup";

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(CleanupEvaluationWorkflowStep));

        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        _logger.LogInformation(
            "MediaFiles count: {Count}",
            state.MediaFiles.Count);

        cancellationToken
            .ThrowIfCancellationRequested();

        if (state.CleanupResult is not null)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Evaluating cleanup");

        foreach (var mediaFile in state.MediaFiles)
        {
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
            "Cleanup completed. Keep: {Keep} Delete: {Delete}",
            result.KeepCount,
            result.DeleteCount);

        return Task.CompletedTask;
    }
}