using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoStabilizationWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            VideoStabilizationWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoStabilizationWorkflowStep);

    public VideoStabilizationWorkflowStep(
        ILogger<VideoStabilizationWorkflowStep> logger)
    {
        _logger =
            logger;
    }

    public override Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return Task.CompletedTask;
        }

        if (!state.EnableStabilization)
        {
            _logger.LogInformation(
                "Skipping stabilization");

            return Task.CompletedTask;
        }

        const string filter =
            "vidstabtransform=smoothing=10";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added stabilization filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}