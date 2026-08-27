namespace ITMartinRedigerDokument.Server.Data.Entities;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid? MainTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Who's responsible for this task - "" means unclaimed. A single owner
    // (not a set like Club's AssignedToNames) since this app is built for a
    // small team where "who's on the hook for this" needs a single clear
    // answer, not a shared claim.
    public string AssignedToName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public MainTask? MainTask { get; set; }
    public List<Reference> References { get; set; } = [];
}
