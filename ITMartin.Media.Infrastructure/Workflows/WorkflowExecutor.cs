// File: ITMartin.Media.Infrastructure/Workflows/WorkflowExecutor.cs

using ITMartin.Media.Application.Abstractions.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class WorkflowExecutor(
    IWorkflowCheckpointStore workflowCheckpointStore,
    IWorkflowStepExecutionStore workflowStepExecutionStore,
    IWorkflowInstanceStore workflowInstanceStore,
    ILogger<WorkflowExecutor> logger)
    : IWorkflowExecutor
{

    public async Task ExecuteAsync<TState>(
    IWorkflowDefinition workflow,
    WorkflowExecutionContext<TState> context,
    CancellationToken cancellationToken = default)
    where TState : class
{
    var workflowId = context.WorkflowId;

    var existingInstance =
        await workflowInstanceStore.ExistsAsync(
            workflowId,
            cancellationToken);

    if (!existingInstance)
    {
        await workflowInstanceStore.CreateAsync(
            workflowId,
            workflow.Name,
            cancellationToken);
    }

    var steps = workflow.Steps.ToList();

    logger.LogInformation(
        "Workflow {WorkflowId} starting execution",
        workflowId);

    for (var i = 0; i < steps.Count; i++)
    {
        var step = steps[i];

        var alreadyCompleted =
            await workflowStepExecutionStore.IsCompletedAsync(
                workflowId,
                step.Name,
                cancellationToken);

        if (alreadyCompleted)
        {
            logger.LogInformation(
                "Skipping already completed step {StepName}",
                step.Name);

            continue;
        }

        logger.LogInformation(
            "Executing workflow step {StepName}",
            step.Name);

        await workflowInstanceStore.SetRunningStepAsync(
            workflowId,
            step.Name,
            cancellationToken);

        await workflowStepExecutionStore.MarkStartedAsync(
            workflowId,
            step.Name,
            cancellationToken);

        try
        {
            await step.ExecuteAsync(
                context,
                cancellationToken);

            await workflowStepExecutionStore.MarkCompletedAsync(
                workflowId,
                step.Name,
                cancellationToken);

            await workflowCheckpointStore.SaveCheckpointAsync(
                workflowId,
                workflow.Name,
                step.Name,
                context.State,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await workflowInstanceStore.MarkFailedAsync(
                workflowId,
                ex.Message,
                cancellationToken);

            logger.LogError(
                ex,
                "Workflow step failed {StepName}",
                step.Name);

            throw;
        }
    }

    await workflowInstanceStore.MarkCompletedAsync(
        workflowId,
        cancellationToken);

    logger.LogInformation(
        "Workflow {WorkflowId} completed",
        workflowId);
}
}