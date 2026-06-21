namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class WorkflowInstanceEntity
{
    public Guid WorkflowId { get; set; }

    public required string WorkflowName { get; set; }

    public required string Status { get; set; }

    public string? CurrentStep { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public int? ProgressCurrent { get; set; }

    public int? ProgressTotal { get; set; }

    public string? ProgressItem { get; set; }

    public string? ProgressData { get; set; }
}