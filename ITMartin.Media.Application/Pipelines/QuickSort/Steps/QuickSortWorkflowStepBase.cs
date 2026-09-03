using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public abstract class QuickSortWorkflowStepBase
    : IWorkflowStep
{
    public abstract string Name { get; }

    public abstract Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;

    protected static async Task<bool> ExecuteOperationAsync(
        string operationName,
        string itemName,
        Func<Task> operation,
        ILogger logger)
    {
        var stopwatch =
            Stopwatch.StartNew();

        // Debug, not Information - this fires twice per file per step, which
        // across tens of thousands of files and 18 steps produced millions
        // of log lines for a single real run (found 2026-08-25 on mie's
        // library) without adding anything LogStepProgress's one-line-per-file
        // percentage log doesn't already say. Still visible with Debug-level
        // logging enabled for troubleshooting a specific run.
        logger.LogDebug(
            """
            --------------------------------------------------
            START OPERATION
            Item: {Item}
            Operation: {Operation}
            --------------------------------------------------
            """,
            itemName,
            operationName);

        try
        {
            await operation();

            stopwatch.Stop();

            logger.LogDebug(
                """
                COMPLETE OPERATION
                Item: {Item}
                Operation: {Operation}
                Duration: {Duration}
                --------------------------------------------------
                """,
                itemName,
                operationName,
                stopwatch.Elapsed);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(
                ex,
                """
                ##################################################
                OPERATION FAILED
                Item: {Item}
                Operation: {Operation}
                Duration: {Duration}
                ##################################################
                """,
                itemName,
                operationName,
                stopwatch.Elapsed);

            return false;
        }
    }

    protected static void LogStepProgress(
        ILogger logger,
        string stepName,
        int current,
        int total,
        string message = "")
    {
        var percent =
            total == 0
                ? 0
                : (double)current / total * 100;

        logger.LogInformation(
            "[{Step}] {Current}/{Total} ({Percent:F1}%) {Message}",
            stepName,
            current,
            total,
            percent,
            message);
    }
}