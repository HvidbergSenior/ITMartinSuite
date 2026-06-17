using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
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

        // Profile-specific color grading.
        // Tape sources are faded and often have a warm (yellow/amber) cast.
        // colorbalance adjusts R/G/B shadows to counter the typical tape warmth.
        var filter = state.RestorationProfile switch
        {
            // Hi8: washed-out, slight warm cast — boost saturation and gamma, cool shadows slightly
            RestorationProfile.Hi8 =>
                "eq=contrast=1.10:brightness=0.02:saturation=1.35:gamma=1.08," +
                "colorbalance=rs=-0.03:gs=-0.01:bs=0.05",

            // VHS: more faded and warmer than Hi8
            RestorationProfile.VHS =>
                "eq=contrast=1.10:brightness=0.03:saturation=1.25:gamma=1.05," +
                "colorbalance=rs=-0.05:gs=-0.02:bs=0.06",

            // VHS Aggressive: very faded, strong correction needed
            RestorationProfile.VHSAggressive =>
                "eq=contrast=1.15:brightness=0.04:saturation=1.45:gamma=1.10," +
                "colorbalance=rs=-0.06:gs=-0.02:bs=0.08",

            // Family Archive: mix of sources, mild lift
            RestorationProfile.FamilyArchive =>
                "eq=contrast=1.07:brightness=0.02:saturation=1.15:gamma=1.03",

            // Handheld: generally good quality, light touch
            RestorationProfile.HandheldCamera =>
                "eq=contrast=1.05:brightness=0.01:saturation=1.10",

            _ =>
                "eq=contrast=1.05:brightness=0.02:saturation=1.08"
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
