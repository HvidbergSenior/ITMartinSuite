namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class WorkflowExecutionSummary
{
    public Guid WorkflowId { get; init; }

    public string WorkflowName { get; init; } = null!;

    public string CurrentStep { get; init; } = null!;

    public bool Completed { get; init; }

    public bool Failed { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}