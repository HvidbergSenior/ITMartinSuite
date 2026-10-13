namespace ITMartinClub.Domain;

public sealed class Idea : ITeamOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProposedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
