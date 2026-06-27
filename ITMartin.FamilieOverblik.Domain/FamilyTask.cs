namespace ITMartin.FamilieOverblik.Domain;

public class FamilyTask
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
}
