using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioNoiseReductionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioEnhancementService
        _audioEnhancementService;

    public override string Name =>
        nameof(AudioNoiseReductionWorkflowStep);

    public AudioNoiseReductionWorkflowStep(
        IAudioEnhancementService audioEnhancementService)
    {
        _audioEnhancementService =
            audioEnhancementService;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video &&
                         x.AudioWorkingPath is not null &&
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    item.AudioWorkingPath =
                        await _audioEnhancementService
                            .ReduceNoiseAsync(
                                item.AudioWorkingPath!,
                                state.RestorationProfile,
                                cancellationToken);
                });
        }
    }
}