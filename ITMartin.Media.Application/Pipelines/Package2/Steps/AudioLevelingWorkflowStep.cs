using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioLevelingWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            AudioLevelingWorkflowStep>
        _logger;

    public string Name =>
        nameof(AudioLevelingWorkflowStep);

    public AudioLevelingWorkflowStep(
        ILogger<AudioLevelingWorkflowStep> logger)
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

        if (!state.EnableAudioNormalize)
        {
            _logger.LogInformation(
                "Skipping audio leveling");

            return Task.CompletedTask;
        }

        const string filter =
            "loudnorm=I=-16:TP=-1.5:LRA=11";

        state.AudioPipeline.Add(filter);

        _logger.LogInformation(
            "Added audio leveling filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }
}