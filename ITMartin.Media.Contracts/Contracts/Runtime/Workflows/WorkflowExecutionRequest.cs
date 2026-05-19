namespace ITMartin.Media.Runtime.Workflows;

public sealed class WorkflowExecutionRequest
{
    public Guid WorkflowId { get; init; }

    public required string WorkflowName { get; init; }

    public required string CorrelationId { get; init; }

    public Dictionary<string, string> Input { get; init; } = [];
}