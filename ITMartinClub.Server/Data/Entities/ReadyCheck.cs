namespace ITMartinClub.Server.Data.Entities;

// A lightweight, ephemeral "is anyone ready to play right now" ping - distinct
// from a scheduled CalendarEvent. Fires a push to the whole group, then shows
// a live list of who's responded. Auto-expires (UI hides it) after ExpiresAt,
// no cleanup job needed since it's just filtered out of the query.
public sealed class ReadyCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int Minutes { get; set; } = 10;
    public string? Phrase { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);

    public List<ReadyCheckResponse> Responses { get; set; } = [];
}
