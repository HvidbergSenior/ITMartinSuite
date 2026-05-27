using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class DuplicateDetectionWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly IDuplicateService
        _duplicateService;

    private readonly ILogger<
            DuplicateDetectionWorkflowStep>
        _logger;

    public DuplicateDetectionWorkflowStep(
        IDuplicateService duplicateService,
        ILogger<DuplicateDetectionWorkflowStep> logger)
    {
        _duplicateService =
            duplicateService;

        _logger =
            logger;
    }

    public override string Name =>
        "Duplicates";

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

        if (state.DuplicateGroups.Count > 0)
        {
            _logger.LogInformation(
                "Duplicate groups already built");

            return;
        }

        await ExecuteOperationAsync(
            "DuplicateDetection",
            $"Files={state.MediaFiles.Count}",
            async () =>
            {
                var total =
                    state.MediaFiles.Count;

                var current = 0;

                foreach (var file in state.MediaFiles)
                {
                    current++;

                    LogStepProgress(
                        _logger,
                        Name,
                        current,
                        total,
                        file.FileName);
                }

                state.DuplicateGroups =
                    _duplicateService
                        .BuildDuplicateGroups(
                            state.MediaFiles);

                _logger.LogInformation(
                    """
                    Duplicate detection completed
                    Groups: {Groups}
                    Duplicate Files: {Duplicates}
                    """,
                    state.DuplicateGroups.Count,
                    state.DuplicateGroups.Sum(x =>
                        x.Files.Count - 1));

                await Task.CompletedTask;
            },
            _logger);
    }
}