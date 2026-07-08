namespace ITMartinUret.Server.Data.Entities;

public enum ReportStatus { Open, Dismissed, ActionTaken }

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }

    public string Reason { get; set; } = "";
    public string? ReporterContact { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
