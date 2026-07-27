namespace ITMartinClub.Server.Data.Entities;

// A remembered enemy-team player, matched by exact in-game name. Good enough
// for a casual friend group's own memory - not meant to be a rigorous
// identity system.
public sealed class Opponent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<OpponentTag> Tags { get; set; } = [];
}
