using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class DuplicateDetectionWorkflowStep
    : IWorkflowStep
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

    public string Name => "Duplicates";

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(DuplicateDetectionWorkflowStep));
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        if (state.DuplicateGroups.Count > 0)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Building duplicate groups");

        state.DuplicateGroups =
            _duplicateService
                .BuildDuplicateGroups(
                    state.MediaFiles);

        _logger.LogInformation(
            "Detected {Count} duplicate groups",
            state.DuplicateGroups.Count);

        return Task.CompletedTask;
    }
}