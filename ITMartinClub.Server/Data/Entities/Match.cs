namespace ITMartinClub.Server.Data.Entities;

// A single R6 round/match. Bomb stats live here (team-level) rather than on
// MatchPlayerStat since who plants varies match to match. "Evening" is not
// its own entity - it's just every Match with the same CreatedAt calendar
// day, same convention SessionNote/SessionRecap already use.
public sealed class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public string? Bombsite { get; set; }
    public bool Won { get; set; }
    public int BombAttempts { get; set; }
    public int BombSuccesses { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MatchPlayerStat> PlayerStats { get; set; } = [];
}
