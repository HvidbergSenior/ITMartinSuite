using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public sealed class EnhancedThumbnailWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(EnhancedThumbnailWorkflowStep);

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
                     .Where(x => !x.Failed))
        {
            item.Operations.Add(
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Success = true
                });
        }

        await Task.CompletedTask;
    }
}