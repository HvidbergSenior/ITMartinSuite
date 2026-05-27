using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDenoiseWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            VideoDenoiseWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoDenoiseWorkflowStep);

    public VideoDenoiseWorkflowStep(
        ILogger<VideoDenoiseWorkflowStep> logger)
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

        if (!state.EnableDenoise)
        {
            _logger.LogInformation(
                "Skipping denoise");

            return Task.CompletedTask;
        }

        const string filter =
            "hqdn3d=2:2:1.5:1.5";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added denoise filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}