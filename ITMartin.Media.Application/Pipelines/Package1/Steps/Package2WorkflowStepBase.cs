using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public abstract class Package1WorkflowStepBase
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

        logger.LogInformation(
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

            logger.LogInformation(
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