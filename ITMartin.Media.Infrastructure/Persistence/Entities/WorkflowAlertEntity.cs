namespace ITMartin.Media.Infrastructure.Persistence.Entities;

/// <summary>
/// A workflow failure/completion event queued for delivery to whoever is
/// running the app. Worker and Server are separate processes that only
/// share the media DB (not each other's DI container), so a failing
/// workflow can't call FileSorterPushService directly - it writes a row
/// here instead, and FileSorter.Server's WorkflowAlertPushHostedService
/// polls for unsent rows and turns them into a Web Push notification.
/// </summary>
public sealed class WorkflowAlertEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string WorkflowId { get; set; }

    public required string WorkflowName { get; set; }

    public required string Kind { get; set; } // "Failed" or "Completed"

    public required string Message { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SentAtUtc { get; set; }
}
