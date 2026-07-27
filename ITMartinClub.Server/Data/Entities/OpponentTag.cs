namespace ITMartinClub.Server.Data.Entities;

// A running timeline of tags on an Opponent, not a single overwritten field -
// so "tagged Sus in June, tagged Chill after a rematch in July" both stay
// visible instead of the later tag silently replacing the earlier one.
public sealed class OpponentTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OpponentId { get; set; }
    public string Preset { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string AddedByName { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Opponent Opponent { get; set; } = null!;
}
