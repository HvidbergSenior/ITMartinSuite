namespace ITMartinBarTab.Server.Data.Entities;

public sealed class DrinkEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid AddedByParticipantId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public decimal Price { get; set; }
    public bool IsRound { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;
    public Participant AddedBy { get; set; } = null!;
    public List<DrinkShare> Shares { get; set; } = [];
}
