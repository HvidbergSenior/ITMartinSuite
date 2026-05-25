using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoSharpenWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            VideoSharpenWorkflowStep>
        _logger;

    public string Name =>
        nameof(VideoSharpenWorkflowStep);

    public VideoSharpenWorkflowStep(
        ILogger<VideoSharpenWorkflowStep> logger)
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

        if (!state.EnableSharpen)
        {
            _logger.LogInformation(
                "Skipping sharpen");

            return Task.CompletedTask;
        }

        var filter =
            BuildFilter(state);

        state.VideoPipeline.Add(filter);

        _logger.LogInformation(
            "Added sharpen filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }

    private static string BuildFilter(
        Package2WorkflowState state)
    {
        return state.RestorationProfile switch
        {
            RestorationProfile.VHSAggressive =>
                "unsharp=5:5:0.8",

            RestorationProfile.FamilyArchive =>
                "unsharp=3:3:0.3",

            RestorationProfile.Hi8 =>
                "unsharp=3:3:0.2",

            _ =>
                "unsharp=3:3:0.3"
        };
    }
}