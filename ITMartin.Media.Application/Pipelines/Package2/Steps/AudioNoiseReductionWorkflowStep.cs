using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioNoiseReductionWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            AudioNoiseReductionWorkflowStep>
        _logger;

    public string Name =>
        nameof(AudioNoiseReductionWorkflowStep);

    public AudioNoiseReductionWorkflowStep(
        ILogger<AudioNoiseReductionWorkflowStep> logger)
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

        if (!state.EnableAudioNoiseReduction)
        {
            _logger.LogInformation(
                "Skipping audio noise reduction");

            return Task.CompletedTask;
        }

        const string filter =
            "afftdn=nr=12:nf=-25";

        state.AudioPipeline.Add(filter);

        _logger.LogInformation(
            "Added audio noise reduction filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }
}