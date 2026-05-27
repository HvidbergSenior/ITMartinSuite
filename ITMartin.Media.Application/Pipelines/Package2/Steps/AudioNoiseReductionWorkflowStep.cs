using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioNoiseReductionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            AudioNoiseReductionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioNoiseReductionWorkflowStep);

    public AudioNoiseReductionWorkflowStep(
        ILogger<AudioNoiseReductionWorkflowStep> logger)
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

        if (!state.EnableAudioNoiseReduction)
        {
            _logger.LogInformation(
                "Skipping audio noise reduction");

            return Task.CompletedTask;
        }

        const string filter =
            "afftdn=nr=12:nf=-25";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.AudioFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added audio noise reduction filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}