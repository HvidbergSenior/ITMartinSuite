using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDeinterlaceWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
        VideoDeinterlaceWorkflowStep> _logger;

    public string Name =>
        nameof(VideoDeinterlaceWorkflowStep);

    public VideoDeinterlaceWorkflowStep(
        ILogger<VideoDeinterlaceWorkflowStep> logger)
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

        if (!state.EnableDeinterlace)
        {
            _logger.LogInformation(
                "Skipping deinterlace step");

            return Task.CompletedTask;
        }

        var filter =
            BuildFilter(state);

        state.VideoPipeline.Add(filter);

        _logger.LogInformation(
            "Added deinterlace filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }

    private static string BuildFilter(
        Package2WorkflowState state)
    {
        return state.DeinterlaceMethod switch
        {
            DeinterlaceMethod.Bwdif =>
                "bwdif=mode=send_frame",

            DeinterlaceMethod.Yadif =>
                "yadif=0:-1:0",

            _ =>
                "bwdif=mode=send_frame"
        };
    }
}