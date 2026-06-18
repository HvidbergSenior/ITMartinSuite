namespace ITMartinFamily.Domain.Entities;

public sealed class DailyTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Note { get; set; } = "";
    public string? ImagePath { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
