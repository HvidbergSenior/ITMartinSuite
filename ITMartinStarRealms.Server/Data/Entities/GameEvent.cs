namespace ITMartinStarRealms.Server.Data.Entities;

// One row per point change, so every player can see who changed what and when -
// the shared scoreboard is self-serve (you can only touch your own points), so
// this log is what answers "wait, why did my number change?" after the fact.
public sealed class GameEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string PlayerAvatar { get; set; } = "🚀";
    public int Delta { get; set; }
    public int ResultingPoints { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
