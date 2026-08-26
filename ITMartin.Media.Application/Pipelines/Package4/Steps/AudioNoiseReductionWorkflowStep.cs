using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class AudioNoiseReductionWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<AudioNoiseReductionWorkflowStep> _logger;
    public override string Name => nameof(AudioNoiseReductionWorkflowStep);

    public AudioNoiseReductionWorkflowStep(ILogger<AudioNoiseReductionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableAudioNoiseReduction)
        {
            _logger.LogInformation("Skipping audio noise reduction");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("afftdn=nf=-25");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
