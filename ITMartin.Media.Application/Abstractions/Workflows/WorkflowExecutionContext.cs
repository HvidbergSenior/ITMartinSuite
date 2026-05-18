// File: ITMartin.Media.Application/Workflows/WorkflowExecutionContext.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public sealed class WorkflowExecutionContext
{
    public Guid WorkflowId { get; init; }

    public required string WorkflowName { get; init; }

    public Dictionary<string, object?> Items { get; init; } = [];

    public CancellationToken CancellationToken { get; init; }
}