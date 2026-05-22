using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioNoiseReductionWorkflowStep
    : IWorkflowStep
{
    private readonly IAudioEnhancementService
        _audioEnhancementService;

    public string Name =>
        nameof(AudioNoiseReductionWorkflowStep);

    public AudioNoiseReductionWorkflowStep(
        IAudioEnhancementService audioEnhancementService)
    {
        _audioEnhancementService =
            audioEnhancementService;
    }

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
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
            var operation =
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt = DateTimeOffset.UtcNow
                };

            try
            {
                item.CurrentWorkingPath =
                    await _audioEnhancementService
                        .ReduceNoiseAsync(
                            item.CurrentWorkingPath!,
                            cancellationToken);

                operation.Success = true;
            }
            catch (Exception ex)
            {
                item.Failed = true;

                item.FailureReason =
                    ex.Message;

                operation.Success = false;

                operation.Metadata =
                    ex.ToString();
            }

            operation.CompletedAt =
                DateTimeOffset.UtcNow;

            item.Operations.Add(
                operation);
        }
    }
}