namespace ITMartinTestHub.Server.Data.Entities;

public sealed class StepResult
{
    public Guid       Id               { get; set; } = Guid.NewGuid();
    public Guid       TestAssignmentId { get; set; }
    public Guid       TestStepId       { get; set; }
    public StepStatus Status           { get; set; }
    public string?    Note             { get; set; }
    public DateTime   CreatedAt        { get; set; } = DateTime.UtcNow;
}

public enum StepStatus { Pass, Fail, Skip }
