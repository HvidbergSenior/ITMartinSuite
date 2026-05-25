using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioSpeechEnhancementWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            AudioSpeechEnhancementWorkflowStep>
        _logger;

    public string Name =>
        nameof(AudioSpeechEnhancementWorkflowStep);

    public AudioSpeechEnhancementWorkflowStep(
        ILogger<AudioSpeechEnhancementWorkflowStep> logger)
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

        if (!state.EnableAiEnhancement)
        {
            _logger.LogInformation(
                "Skipping speech enhancement");

            return Task.CompletedTask;
        }

        const string filter =
            "arnndn=m=std.rnnn";

        state.AudioPipeline.Add(filter);

        _logger.LogInformation(
            "Added speech enhancement filter: {Filter}",
            filter);

        return Task.CompletedTask;
    }
}