using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioLevelingWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioEnhancementService
        _audioEnhancementService;

    public override string Name =>
        nameof(AudioLevelingWorkflowStep);

    public AudioLevelingWorkflowStep(
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
                         x.CurrentWorkingPath is not null))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    item.CurrentWorkingPath =
                        await _audioEnhancementService
                            .NormalizeAudioAsync(
                                item.CurrentWorkingPath!,
                                cancellationToken);
                });
        }
    }
}