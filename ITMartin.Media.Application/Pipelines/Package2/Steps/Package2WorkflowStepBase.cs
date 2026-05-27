using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public abstract class Package2WorkflowStepBase
    : IWorkflowStep
{
    public abstract string Name { get; }

    public abstract Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;

    protected static async Task ExecuteOperationAsync(
        EnhancedMediaItem item,
        string operationName,
        Func<Task> operation,
        ILogger logger)
    {
        var stopwatch =
            Stopwatch.StartNew();

        item.Processing = true;

        item.CurrentOperation =
            operationName;

        item.StartedAt ??=
            DateTimeOffset.UtcNow;

        logger.LogInformation(
            """
            --------------------------------------------------
            START OPERATION
            Item: {Item}
            Operation: {Operation}
            --------------------------------------------------
            """,
            item.OriginalPath,
            operationName);

        var enhancementOperation =
            new EnhancementOperation
            {
                Name = operationName,
                StartedAt =
                    DateTimeOffset.UtcNow
            };

        try
        {
            await operation();

            enhancementOperation.Success =
                true;

            stopwatch.Stop();

            item.CurrentOperation =
                null;

            logger.LogInformation(
                """
                COMPLETE OPERATION
                Item: {Item}
                Operation: {Operation}
                Duration: {Duration}
                --------------------------------------------------
                """,
                item.OriginalPath,
                operationName,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            item.Failed = true;

            item.FailureReason =
                ex.Message;

            item.CurrentOperation =
                null;

            enhancementOperation.Success =
                false;

            enhancementOperation.Metadata =
                ex.ToString();

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
                item.OriginalPath,
                operationName,
                stopwatch.Elapsed);
        }

        enhancementOperation.CompletedAt =
            DateTimeOffset.UtcNow;

        item.Operations.Add(
            enhancementOperation);

        item.CompletedAt =
            DateTimeOffset.UtcNow;

        item.Processing =
            false;
    }

    protected static bool AlreadyExecuted(
        EnhancedMediaItem item,
        string operationName)
    {
        return item.Operations.Any(o =>
            o.Name == operationName &&
            o.Success);
    }
}