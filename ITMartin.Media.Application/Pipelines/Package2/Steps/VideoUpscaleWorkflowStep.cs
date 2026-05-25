using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoUpscaleWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
        VideoUpscaleWorkflowStep> _logger;

    public override string Name =>
        nameof(VideoUpscaleWorkflowStep);

    public VideoUpscaleWorkflowStep(
        ILogger<VideoUpscaleWorkflowStep> logger)
    {
        _logger = logger;
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
                "Skipping video upscale");

            return Task.CompletedTask;
        }

        var filter =
            BuildFilter(state);

        state.VideoPipeline.Add(filter);

        _logger.LogInformation(
            "Added upscale filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }

    private static string BuildFilter(
        Package2WorkflowState state)
    {
        return state.TargetHeight switch
        {
            720 => "scale=-2:720",
            1080 => "scale=-2:1080",
            1440 => "scale=-2:1440",
            2160 => "scale=-2:2160",
            _ => "scale=-2:1080"
        };
    }
}