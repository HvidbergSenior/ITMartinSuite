namespace ITMartinClub.Server.Data.Entities;

// Ported from the standalone Family app - a per-person "don't forget" note
// tied to a date, not a shared Assignment (no assignee/claim workflow, just
// a personal list each member keeps for themselves).
public sealed class PersonalReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool Done { get; set; }
    public string? PhotoFileName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
}
