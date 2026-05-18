// File: ITMartin.Media.Infrastructure/Workflows/WorkflowExecutor.cs

using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class WorkflowExecutor(
    IWorkflowStateStore workflowStateStore)
    : IWorkflowExecutor
{
    public async Task ExecuteAsync(
        IWorkflowDefinition workflow,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var lastCompletedStep =
            await workflowStateStore.GetLastCompletedStepAsync(
                context.WorkflowId,
                cancellationToken);

        var resume = string.IsNullOrWhiteSpace(lastCompletedStep);

        foreach (var step in workflow.Steps)
        {
            if (!resume)
            {
                if (step.Name == lastCompletedStep)
                {
                    resume = true;
                }

                continue;
            }

            await step.ExecuteAsync(
                context,
                cancellationToken);

            await workflowStateStore.SaveCheckpointAsync(
                context.WorkflowId,
                step.Name,
                cancellationToken);
        }
    }
}