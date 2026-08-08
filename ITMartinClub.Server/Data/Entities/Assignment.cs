namespace ITMartinClub.Server.Data.Entities;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }

    // NotStarted / InProgress / Done. IsCompleted below is kept in sync
    // (Status == "Done") so existing reads elsewhere don't need to change.
    public string Status { get; set; } = "NotStarted";
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CompletedByName { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
}
