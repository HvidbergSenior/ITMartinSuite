namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class WorkflowCheckpointEntity
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;

    public string StepName { get; set; } = string.Empty;

    public string StateJson { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int Attempt { get; set; }

    public bool IsLatest { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ErrorDetails { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}