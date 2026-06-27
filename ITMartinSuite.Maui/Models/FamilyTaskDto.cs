namespace ITMartinSuite.Maui.Models;

public class FamilyTaskDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "Task";
    public string? Note { get; set; }
    public string? PhotoPath { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ClaimedBy { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string StatusText => IsCompleted
        ? $"Done by {ClaimedBy ?? CreatedBy}"
        : ClaimedBy is not null
            ? $"→ {ClaimedBy}"
            : "";
}
