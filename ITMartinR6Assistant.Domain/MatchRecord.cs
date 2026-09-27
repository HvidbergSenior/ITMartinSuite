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
    // Rounds split by side - the map-ban vote cares whether we win a map
    // on attack, on defence, or both.
    public int AtkWon { get; set; }
    public int AtkLost { get; set; }
    public int DefWon { get; set; }
    public int DefLost { get; set; }
    public int RoundsWon => AtkWon + DefWon;
    public int RoundsLost => AtkLost + DefLost;
    public string Note { get; set; } = "";
    // The rounds as tapped during the match (side + site + won) - feeds the per-site win %. Empty for older logs.
    public List<RoundResult> Rounds { get; set; } = new();
}

// Aggregate over logged matches for a map or a site.
public sealed record MapStats(int Played, int Won, int AtkWon, int AtkLost, int DefWon, int DefLost, DateTime? LastPlayedUtc)
{
    public int WinPct => Played > 0 ? 100 * Won / Played : 0;
    public int AtkPct => AtkWon + AtkLost > 0 ? 100 * AtkWon / (AtkWon + AtkLost) : 0;
    public int DefPct => DefWon + DefLost > 0 ? 100 * DefWon / (DefWon + DefLost) : 0;
    public int RoundPct => AtkWon + DefWon + AtkLost + DefLost > 0 ? 100 * (AtkWon + DefWon) / (AtkWon + DefWon + AtkLost + DefLost) : 0;
    public static readonly MapStats Empty = new(0, 0, 0, 0, 0, 0, null);
}
