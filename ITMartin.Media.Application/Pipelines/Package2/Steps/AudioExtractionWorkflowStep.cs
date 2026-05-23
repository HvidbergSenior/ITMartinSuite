using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioExtractionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioExtractionService
        _audioExtractionService;

    public override string Name =>
        nameof(AudioExtractionWorkflowStep);

    public AudioExtractionWorkflowStep(
        IAudioExtractionService audioExtractionService)
    {
        _audioExtractionService =
            audioExtractionService;
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
                         x.CurrentWorkingPath is not null &&
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
                        await _audioExtractionService
                            .ExtractAsync(
                                item.CurrentWorkingPath!,
                                cancellationToken);
                });
        }
    }
}