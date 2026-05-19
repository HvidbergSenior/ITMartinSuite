namespace ITMartin.Media.Application.Models.Workflows;

public sealed class WorkflowStepExecution
{
    public Guid Id { get; init; }

    public Guid WorkflowId { get; init; }

    public required string StepName { get; init; }

    public required string Status { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }
}