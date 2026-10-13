namespace ITMartinClub.Domain;

public sealed class Vehicle : ITeamOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Availability { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
