namespace ITMartinClub.Server.Data.Entities;

// A member proposing "actually, let's move it to 7" instead of just the admin
// unilaterally editing - other members vote on it (EventTimeVote), then an
// admin applies or dismisses it against the real CalendarEvent.
public sealed class EventTimeSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public DateTime SuggestedDate { get; set; }
    public string SuggestedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(2); // "vote within 2 hours" - stale after that
    public bool Resolved { get; set; }
}
