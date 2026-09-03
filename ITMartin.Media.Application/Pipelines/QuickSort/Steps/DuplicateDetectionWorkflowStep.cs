using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class DuplicateDetectionWorkflowStep
    : QuickSortWorkflowStepBase
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
            context.State as QuickSortWorkflowState
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

        if (!state.EnableDeduplication)
        {
            _logger.LogInformation(
                "Duplicate detection disabled for this run - every file is kept");

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
                    await _duplicateService
                        .BuildDuplicateGroupsAsync(
                            state.MediaFiles,
                            cancellationToken);

                foreach (var group in state.DuplicateGroups)
                {
                    // Keep the first (oldest by path), mark the rest as duplicates
                    foreach (var dup in group.Files.Skip(1))
                        dup.ExportSubFolder = "Duplicates";
                }

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