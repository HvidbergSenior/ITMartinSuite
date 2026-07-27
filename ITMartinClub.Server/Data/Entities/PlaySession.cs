namespace ITMartinClub.Server.Data.Entities;

// One shared, group-wide play session moving through three explicit phases.
// Phase only ever changes via an explicit button click (see Ready.razor) -
// never inferred automatically from time or activity.
public sealed class PlaySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Phase { get; set; } = "Invitations"; // Invitations | Playing | Recap
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PlayingStartedAt { get; set; }
    public DateTime? RecapStartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
