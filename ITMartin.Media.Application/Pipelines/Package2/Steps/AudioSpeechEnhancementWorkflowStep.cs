using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioSpeechEnhancementWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            AudioSpeechEnhancementWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioSpeechEnhancementWorkflowStep);

    public AudioSpeechEnhancementWorkflowStep(
        ILogger<AudioSpeechEnhancementWorkflowStep> logger)
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

        if (!state.EnableAiEnhancement)
        {
            _logger.LogInformation(
                "Skipping speech enhancement");

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
                        "arnndn=m=std.rnnn";

                    item.AudioFilters.Add(filter);


                    _logger.LogInformation(
                        "Added speech enhancement filter: {Filter}",
                        filter);

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}