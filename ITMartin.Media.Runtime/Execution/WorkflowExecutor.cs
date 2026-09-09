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
    IWorkflowAlertNotifier workflowAlertNotifier,
    ActiveWorkflowRegistry activeWorkflowRegistry,
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

        // The single chokepoint every start path goes through (queue
        // consumer and recovery service alike) - see ActiveWorkflowRegistry
        // for the duplicate-execution incident this prevents.
        if (!activeWorkflowRegistry.TryEnter(workflowId))
        {
            logger.LogWarning(
                "Workflow {WorkflowId} is already executing in this process - ignoring duplicate start request",
                workflowId);

            return;
        }

        try
        {
            await ExecuteCoreAsync(
                workflow,
                context,
                cancellationToken);
        }
        finally
        {
            activeWorkflowRegistry.Exit(workflowId);
        }
    }

    private async Task ExecuteCoreAsync<TState>(
        IWorkflowDefinition workflow,
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken)
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
        else
        {
            // Re-entering an existing instance means recovery picked it up,
            // so its row still says Running-but-dead or Failed. Clear that
            // now: otherwise the whole re-run reports the stale status and a
            // live workflow looks like a failed one to every consumer.
            await workflowInstanceStore.MarkRunningAsync(
                workflowId,
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

                // Checkpoint FIRST, then mark the step completed. These are
                // two separate tables, and resume reads them independently:
                // the skip decision above comes from the step-execution
                // store, while the state comes from the latest checkpoint. If
                // the process dies between them the two disagree, and the
                // order decides which way.
                //
                // Marking completed first fails dangerously: the step is
                // skipped on resume while its output was never checkpointed,
                // so every later step runs against state missing that step's
                // work - silently, with no error. That is what produced
                // "0 files tracked" from FileStatus on the ToshibaTest run
                // 2026-09-09, after 41,176 files had actually been exported.
                //
                // Checkpointing first fails safely: the step simply runs
                // again on resume, which recovery already assumes steps
                // tolerate.
                await workflowCheckpointStore
                    .SaveCheckpointAsync(
                        workflowId,
                        workflow.Name,
                        step.Name,
                        context.State,
                        cancellationToken);

                await workflowStepExecutionStore
                    .MarkCompletedAsync(
                        workflowId,
                        step.Name,
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

                // Log before attempting to persist - if MarkFailedAsync
                // itself throws (e.g. the DB is locked from the same
                // contention that caused the original failure), the real
                // error must not be lost entirely. Confirmed this exact
                // scenario during the ToshibaTest run 2026-09-08: a step
                // failure's DB write hit 'database is locked', the
                // original exception was never logged anywhere, and
                // FailureReason stayed empty.
                logger.LogError(
                    ex,
                    "Step {StepName} failed for workflow {WorkflowId}",
                    step.Name,
                    workflowId);

                await workflowInstanceStore
                    .MarkFailedAsync(
                        workflowId,
                        ex.Message,
                        cancellationToken);

                // Alerting must never mask the real failure - CancellationToken.None
                // so a caller-cancelled token (e.g. app shutdown) doesn't also
                // swallow this best-effort notification silently mid-throw.
                await workflowAlertNotifier.NotifyFailedAsync(
                    workflowId,
                    workflow.Name,
                    ex.Message,
                    CancellationToken.None);

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

        await workflowAlertNotifier.NotifyCompletedAsync(
            workflowId,
            workflow.Name,
            workflowStopwatch.Elapsed,
            CancellationToken.None);

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