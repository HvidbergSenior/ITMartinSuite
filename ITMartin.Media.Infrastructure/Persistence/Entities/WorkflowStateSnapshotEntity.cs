namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class WorkflowStateSnapshot
{
    public Guid WorkflowId { get; set; }

    public string SerializedContext { get; set; } = null!;

    public DateTimeOffset UpdatedAt { get; set; }
}