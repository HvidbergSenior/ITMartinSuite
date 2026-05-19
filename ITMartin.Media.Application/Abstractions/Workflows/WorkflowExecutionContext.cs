namespace ITMartin.Media.Application.Abstractions.Workflows;

public sealed class WorkflowExecutionContext<TState>
    where TState : class
{
    public Guid WorkflowId { get; init; }

    public required string WorkflowName { get; init; }

    public required TState State { get; init; }

    public CancellationToken CancellationToken { get; init; }
}