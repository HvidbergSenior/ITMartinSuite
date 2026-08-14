using System.ComponentModel.DataAnnotations.Schema;

namespace ITMartinClub.Server.Data.Entities;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid? MainTaskId { get; set; }
    public Guid? StorageLocationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Semicolon-separated member names ("" = unclaimed). Replaces the old
    // single AssignedToName column - a task can have several assignees now,
    // and any one of them can mark it complete. Kept as a flat delimited
    // string rather than a join table since assignee sets are small and are
    // never queried across tasks.
    public string AssignedToNames { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    // Null = show now. Set to a future date to hold the task out of the open
    // list until that day arrives (e.g. "create for tomorrow").
    public DateTime? ScheduledFor { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CompletedByName { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public MainTask? MainTask { get; set; }

    [NotMapped]
    public List<string> Assignees =>
        AssignedToNames.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

    [NotMapped]
    public string AssigneeLabel => string.Join(", ", Assignees);

    public bool IsAssignedTo(string name) => Assignees.Contains(name, StringComparer.OrdinalIgnoreCase);

    public void AddAssignee(string name)
    {
        if (IsAssignedTo(name)) return;
        AssignedToNames = AssignedToNames.Length == 0 ? name : $"{AssignedToNames};{name}";
    }
}
