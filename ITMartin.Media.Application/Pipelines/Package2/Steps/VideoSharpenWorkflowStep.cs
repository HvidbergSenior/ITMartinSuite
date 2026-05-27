using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoSharpenWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            VideoSharpenWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoSharpenWorkflowStep);

    public VideoSharpenWorkflowStep(
        ILogger<VideoSharpenWorkflowStep> logger)
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

        if (!state.EnableSharpen)
        {
            _logger.LogInformation(
                "Skipping sharpen");

            return Task.CompletedTask;
        }

        const string filter =
            "unsharp=5:5:0.8:3:3:0.4";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added sharpen filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}