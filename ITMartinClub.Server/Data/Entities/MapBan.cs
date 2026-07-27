namespace ITMartinClub.Server.Data.Entities;

// Logged independently of Match - a banned map is never played, so it can't
// hang off a Match row. Ban rate is computed as this map's share of all
// logged bans, not against a per-session "ban set" (no such structure exists).
public sealed class MapBan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Map { get; set; } = string.Empty;
    public string BannedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
