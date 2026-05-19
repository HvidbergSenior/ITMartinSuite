namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class WorkflowStepExecutionEntity
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public required string StepName { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}