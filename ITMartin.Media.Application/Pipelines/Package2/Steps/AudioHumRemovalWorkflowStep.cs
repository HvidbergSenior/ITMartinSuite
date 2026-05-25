using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioHumRemovalWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            AudioHumRemovalWorkflowStep>
        _logger;

    public string Name =>
        nameof(AudioHumRemovalWorkflowStep);

    public AudioHumRemovalWorkflowStep(
        ILogger<AudioHumRemovalWorkflowStep> logger)
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

        if (!state.EnableHumRemoval)
        {
            _logger.LogInformation(
                "Skipping hum removal");

            return Task.CompletedTask;
        }

        const string filter =
            "highpass=f=50,lowpass=f=10000";

        state.AudioPipeline.Add(filter);

        _logger.LogInformation(
            "Added hum removal filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }
}