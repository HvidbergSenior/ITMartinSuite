using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.Execution;

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
        var workflowId =
            context.WorkflowId;

        var workflowStopwatch =
            Stopwatch.StartNew();

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

        var steps =
            workflow.Steps.ToList();

        logger.LogInformation(
            """
            ==================================================
            WORKFLOW START
            Workflow: {Workflow}
            WorkflowId: {WorkflowId}
            Total Steps: {TotalSteps}
            ==================================================
            """,
            workflow.Name,
            workflowId,
            steps.Count);

        for (var i = 0; i < steps.Count; i++)
        {
            var step =
                steps[i];

            var stepNumber =
                i + 1;

            var alreadyCompleted =
                await workflowStepExecutionStore
                    .IsCompletedAsync(
                        workflowId,
                        step.Name,
                        cancellationToken);

            if (alreadyCompleted)
            {
                logger.LogInformation(
                    """
                    --------------------------------------------------
                    [{StepNumber}/{TotalSteps}] SKIPPED
                    Step: {StepName}
                    --------------------------------------------------
                    """,
                    stepNumber,
                    steps.Count,
                    step.Name);

                continue;
            }

            logger.LogWorkflowStepStart(
                step.Name,
                stepNumber,
                steps.Count);

            var stepStopwatch =
                Stopwatch.StartNew();

            await workflowInstanceStore
                .SetRunningStepAsync(
                    workflowId,
                    step.Name,
                    cancellationToken);

            await workflowStepExecutionStore
                .MarkStartedAsync(
                    workflowId,
                    step.Name,
                    cancellationToken);

            try
            {
                await step.ExecuteAsync(
                    context,
                    cancellationToken);

                stepStopwatch.Stop();

                await workflowStepExecutionStore
                    .MarkCompletedAsync(
                        workflowId,
                        step.Name,
                        cancellationToken);

                await workflowCheckpointStore
                    .SaveCheckpointAsync(
                        workflowId,
                        workflow.Name,
                        step.Name,
                        context.State,
                        cancellationToken);

                logger.LogWorkflowStepEnd(
                    step.Name,
                    stepNumber,
                    steps.Count,
                    stepStopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stepStopwatch.Stop();

                await workflowInstanceStore
                    .MarkFailedAsync(
                        workflowId,
                        ex.Message,
                        cancellationToken);

                logger.LogError(
                    ex,
                    """
                    ##################################################
                    STEP FAILED
                    Step: {StepName}
                    Duration: {Duration}
                    ##################################################
                    """,
                    step.Name,
                    stepStopwatch.Elapsed);

                throw;
            }
        }

        workflowStopwatch.Stop();

        await workflowInstanceStore
            .MarkCompletedAsync(
                workflowId,
                cancellationToken);

        logger.LogInformation(
            """
            ==================================================
            WORKFLOW COMPLETE
            Workflow: {Workflow}
            WorkflowId: {WorkflowId}
            Total Duration: {Duration}
            ==================================================
            """,
            workflow.Name,
            workflowId,
            workflowStopwatch.Elapsed);
    }
}