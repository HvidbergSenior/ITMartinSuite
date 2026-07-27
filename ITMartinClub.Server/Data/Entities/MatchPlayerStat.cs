namespace ITMartinClub.Server.Data.Entities;

// One squad member's numbers for one Match. "3+ kills" is intentionally not
// stored here - it's fully derivable from Kills >= 3, so it's computed
// wherever it's displayed instead of risking it drifting out of sync with
// the actual Kills value.
public sealed class MatchPlayerStat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public bool LoneSurvivor { get; set; }

    public Match Match { get; set; } = null!;
}
