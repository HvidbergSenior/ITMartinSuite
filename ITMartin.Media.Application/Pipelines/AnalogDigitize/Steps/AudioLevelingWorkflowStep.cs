using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class AudioLevelingWorkflowStep
    : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<
            AudioLevelingWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioLevelingWorkflowStep);

    public AudioLevelingWorkflowStep(
        ILogger<AudioLevelingWorkflowStep> logger)
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

        if (!state.EnableAudioNormalize)
        {
            _logger.LogInformation(
                "Skipping audio leveling");

            return Task.CompletedTask;
        }

        const string filter =
            "loudnorm=I=-16:TP=-1.5:LRA=11";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.AudioFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added audio leveling filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}