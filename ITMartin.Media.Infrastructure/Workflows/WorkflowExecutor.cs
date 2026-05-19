// File: ITMartin.Media.Infrastructure/Workflows/WorkflowExecutor.cs

using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Models.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class WorkflowExecutor(
    IWorkflowCheckpointStore workflowCheckpointStore,
    IWorkflowResumeStore workflowResumeStore,
    IWorkflowStepExecutionStore workflowStepExecutionStore,
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

        var resumeState = await workflowResumeStore.GetAsync(
            workflowId,
            cancellationToken);

        var steps = workflow.Steps.ToList();

        var startIndex = 0;

        if (resumeState?.LastCompletedStep is not null)
        {
            var completedIndex = steps.FindIndex(
                x => x.Name == resumeState.LastCompletedStep);

            if (completedIndex >= 0)
            {
                startIndex = completedIndex + 1;
            }
        }

        logger.LogInformation(
            "Workflow {WorkflowId} resuming from index {StartIndex}",
            workflowId,
            startIndex);

        for (var i = startIndex; i < steps.Count; i++)
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

            await workflowStepExecutionStore.MarkStartedAsync(
                workflowId,
                step.Name,
                cancellationToken);

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
            
            await workflowResumeStore.SaveAsync(
                new WorkflowResumeState(
                    workflowId,
                    step.Name,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        await workflowResumeStore.MarkCompletedAsync(
            workflowId,
            cancellationToken);
    }
}