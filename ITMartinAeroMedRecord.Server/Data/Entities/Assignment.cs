using System.ComponentModel.DataAnnotations.Schema;

namespace ITMartinAeroMedRecord.Server.Data.Entities;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid? MainTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Semicolon-separated member names ("" = unclaimed) - a task can have
    // several assignees, and any one of them can mark it complete. Kept as a
    // flat delimited string rather than a join table since assignee sets are
    // small and never queried across tasks. Same convention as Club.
    public string AssignedToNames { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public MainTask? MainTask { get; set; }
    public List<Reference> References { get; set; } = [];

    [NotMapped]
    public List<string> Assignees =>
        AssignedToNames.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

    [NotMapped]
    public string AssigneeLabel => string.Join(", ", Assignees);

    public bool IsAssignedTo(string name) => Assignees.Contains(name, StringComparer.OrdinalIgnoreCase);
}
