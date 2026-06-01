namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class WorkflowStateSnapshot
{
    public Guid WorkflowId { get; set; }

    public string WorkflowName { get; set; } = null!;

    public string StateType { get; set; } = null!;

    public string SerializedContext { get; set; } = null!;

    public DateTimeOffset UpdatedAt { get; set; }
}