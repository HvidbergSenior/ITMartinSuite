using System.ComponentModel.DataAnnotations;

namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class WorkflowResumeEntity
{
    [Key]
    public Guid WorkflowId { get; set; }

    public string LastCompletedStep { get; set; } = null!;

    public DateTimeOffset UpdatedAtUtc { get; set; }
    // WorkflowResumeEntity.cs
    public bool IsCompleted { get; set; }
}