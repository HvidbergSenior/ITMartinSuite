using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoColorCorrectionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            VideoColorCorrectionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoColorCorrectionWorkflowStep);

    public VideoColorCorrectionWorkflowStep(
        ILogger<VideoColorCorrectionWorkflowStep> logger)
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

        if (!state.EnableColorCorrection)
        {
            _logger.LogInformation(
                "Skipping color correction");

            return Task.CompletedTask;
        }

        const string filter =
            "eq=contrast=1.05:brightness=0.02:saturation=1.08";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added color correction filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}