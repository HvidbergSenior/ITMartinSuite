namespace ITMartinCloudOverblik.Server.Data.Entities;

public sealed class AuditLead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int FamilySize { get; set; }
    public string ServicesJson { get; set; } = "[]";
    public decimal MonthlyCost { get; set; }
    public decimal MonthlySaving { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool Contacted { get; set; }
}
