namespace ITMartinMagazine.Server.Data.Entities;

public class MagazineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string IssueDate { get; set; } = "";
    public int Year { get; set; }
    public string Publisher { get; set; } = "";
    public string Country { get; set; } = "Other";
    public string Condition { get; set; } = "Good";
    public string ValueRating { get; set; } = "Unknown";
    public string AiReasoning { get; set; } = "";
    public string? CoverImagePath { get; set; }
    public string Notes { get; set; } = "";
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}
