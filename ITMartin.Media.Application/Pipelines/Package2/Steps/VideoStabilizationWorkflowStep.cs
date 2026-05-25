using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoStabilizationWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            VideoStabilizationWorkflowStep>
        _logger;

    public string Name =>
        nameof(VideoStabilizationWorkflowStep);

    public VideoStabilizationWorkflowStep(
        ILogger<VideoStabilizationWorkflowStep> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
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

        var filter =
            BuildFilter(state);

        state.VideoPipeline.Add(filter);

        _logger.LogInformation(
            "Added stabilization filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }

    private static string BuildFilter(
        Package2WorkflowState state)
    {
        return
            "vidstabtransform=smoothing=5";
    }
}