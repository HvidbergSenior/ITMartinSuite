using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

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
        Func<Task> operation)
    {
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
        }
        catch (Exception ex)
        {
            item.Failed = true;

            item.FailureReason =
                ex.Message;

            enhancementOperation.Success =
                false;

            enhancementOperation.Metadata =
                ex.ToString();
        }

        enhancementOperation.CompletedAt =
            DateTimeOffset.UtcNow;

        item.Operations.Add(
            enhancementOperation);
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