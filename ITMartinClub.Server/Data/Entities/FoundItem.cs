namespace ITMartinClub.Server.Data.Entities;

// "Where did I put X" log - ported from the standalone ADHD FindIt app so
// every group gets it, not just one person. Deliberately simple: name +
// location + optional note/photo, searchable list. No AI parsing in v1.
public sealed class FoundItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? PhotoFileName { get; set; }
    public string StoredByName { get; set; } = string.Empty;
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
}
