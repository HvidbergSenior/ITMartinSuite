namespace ITMartinClub.Server.Data.Entities;

public sealed class MainTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DefinitionOfDone { get; set; }
    public int SortOrder { get; set; }

    // When true, this main task is a recurring daily checklist rather than a
    // one-off backlog: its subtasks auto-reopen once a new day starts, and
    // "done" means all of today's subtasks are complete, not a fixed text.
    public bool IsDaily { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
}
