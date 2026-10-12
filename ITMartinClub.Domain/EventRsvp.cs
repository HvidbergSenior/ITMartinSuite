namespace ITMartinClub.Server.Data.Entities;

public sealed class EventRsvp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Status { get; set; } = "Going"; // Going, Maybe, Cant
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}
