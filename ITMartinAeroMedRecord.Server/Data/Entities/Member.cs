namespace ITMartinAeroMedRecord.Server.Data.Entities;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;

    // Personal PIN verified at join time so one person can't accidentally (or
    // deliberately) act as a teammate just by typing their name - same
    // convention as Club's Member.Pin.
    public string Pin { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Free-text, self-set by the member (e.g. role, contact info) - shown to
    // teammates on Medlemmer, editable only by the member themselves.
    public string? PersonligInfo { get; set; }

    // Structured profile fields, all self-set and editable only by the
    // member themselves - same permission model as PersonligInfo above.
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Role { get; set; }
    public string? Interests { get; set; }
    public string? PhotoFileName { get; set; }

    public Group Group { get; set; } = null!;
}
