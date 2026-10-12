namespace ITMartinClub.Server.Data.Entities;

// Attached to a CalendarEvent - a short pre-session briefing ("Wednesday 19:30:
// look at this before") with a one-line focus and a simple checklist of what
// to do before joining. One per event (created or edited by any member).
public sealed class EventPrep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string Focus { get; set; } = string.Empty;
    public string Checklist { get; set; } = string.Empty; // newline-separated items
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
