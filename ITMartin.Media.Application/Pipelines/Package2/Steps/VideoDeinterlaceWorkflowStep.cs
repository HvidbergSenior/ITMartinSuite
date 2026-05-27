using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDeinterlaceWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            VideoDeinterlaceWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoDeinterlaceWorkflowStep);

    public VideoDeinterlaceWorkflowStep(
        ILogger<VideoDeinterlaceWorkflowStep> logger)
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

        if (!state.EnableDeinterlace)
        {
            _logger.LogInformation(
                "Skipping deinterlace step");

            return Task.CompletedTask;
        }

        var filter =
            BuildFilter(state);

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added deinterlace filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

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