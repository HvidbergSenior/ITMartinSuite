namespace ITMartinR6Assistant.Domain;

// One logged match: map, site we ended on, result. Logged by hand in the
// PostMatch phase - Ubisoft has no public API, so this is the team's own
// per-map / per-site record and feeds the numbers on /maps.
public class MatchRecord
{
    public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
    public string Map { get; set; } = "";
    public string Site { get; set; } = "";
    public bool Won { get; set; }
    public int RoundsWon { get; set; }
    public int RoundsLost { get; set; }
    public string Note { get; set; } = "";
}
