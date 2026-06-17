using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoUpscaleWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            VideoUpscaleWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoUpscaleWorkflowStep);

    public VideoUpscaleWorkflowStep(
        ILogger<VideoUpscaleWorkflowStep> logger)
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

        if (!state.EnableUpscaling)
        {
            _logger.LogInformation(
                "Skipping upscale");

            return Task.CompletedTask;
        }

        var targetHeight =
            state.Configuration.Video.TargetHeight > 0
                ? state.Configuration.Video.TargetHeight
                : 1080;

        // Lanczos gives sharper results than the default bilinear when upscaling
        var filter =
            $"scale=-2:{targetHeight}:flags=lanczos";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added upscale filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}
