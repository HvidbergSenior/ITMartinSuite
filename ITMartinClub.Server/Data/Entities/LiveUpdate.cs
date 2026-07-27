namespace ITMartinClub.Server.Data.Entities;

public sealed class LiveUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaySessionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? MediaFileName { get; set; } // optional photo/short clip attached to the update
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
