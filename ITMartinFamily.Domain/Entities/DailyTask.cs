namespace ITMartinFamily.Domain.Entities;

public sealed class DailyTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public string Note { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string? AssignedTo { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
