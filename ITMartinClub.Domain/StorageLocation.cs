namespace ITMartinClub.Server.Data.Entities;

public sealed class StorageLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ApproxSize { get; set; }
    public string? Contents { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
