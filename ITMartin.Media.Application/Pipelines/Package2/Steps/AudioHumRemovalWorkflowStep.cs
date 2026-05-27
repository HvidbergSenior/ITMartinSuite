using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioHumRemovalWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            AudioHumRemovalWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioHumRemovalWorkflowStep);

    public AudioHumRemovalWorkflowStep(
        ILogger<AudioHumRemovalWorkflowStep> logger)
    {
        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        if (!state.EnableHumRemoval)
        {
            _logger.LogInformation(
                "Skipping hum removal");

            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    const string filter =
                        "highpass=f=50,lowpass=f=10000";

                    item.VideoFilters.Add(filter);

                    _logger.LogInformation(
                        "Added hum removal filter: {Filter}",
                        filter);

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}