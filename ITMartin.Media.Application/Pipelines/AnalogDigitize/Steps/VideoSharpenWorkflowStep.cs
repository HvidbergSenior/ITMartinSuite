using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class VideoSharpenWorkflowStep
    : AnalogDigitizeWorkflowStepBase
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
        if (context.State is not AnalogDigitizeWorkflowState state)
        {
            return Task.CompletedTask;
        }

        if (!state.EnableSharpen)
        {
            _logger.LogInformation(
                "Skipping sharpen");

            return Task.CompletedTask;
        }

        // For tape sources, sharpen luma only (chroma sharpening adds noise on compressed tape).
        // unsharp: lx:ly:la:cx:cy:ca — luma matrix 5x5, luma amount, chroma off.
        var filter = state.RestorationProfile switch
        {
            RestorationProfile.VHSAggressive =>
                "unsharp=5:5:1.2:0:0:0",

            RestorationProfile.VHS or
            RestorationProfile.Hi8 =>
                "unsharp=5:5:1.0:0:0:0",

            RestorationProfile.FamilyArchive =>
                "unsharp=5:5:0.6:0:0:0",

            _ =>
                "unsharp=5:5:0.8:3:3:0.4"
        };

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
