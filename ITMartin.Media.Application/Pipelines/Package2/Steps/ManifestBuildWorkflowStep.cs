using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ManifestBuildWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(ManifestBuildWorkflowStep);

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        foreach (var item in state.Items)
        {
            item.Operations.Add(
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Success = !item.Failed
                });
        }

        await Task.CompletedTask;
    }
}