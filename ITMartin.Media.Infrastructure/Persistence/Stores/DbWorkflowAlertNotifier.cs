using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

/// <summary>
/// Writes an alert row instead of sending anything directly - Worker (where
/// workflows actually run) and Server (where the VAPID keys and Web Push
/// subscriptions live) are separate processes, and the media DB is the one
/// thing they already share. FileSorter.Server's WorkflowAlertPushHostedService
/// polls this table and does the actual sending. Never lets a notification
/// failure break the workflow it's reporting on.
/// </summary>
public sealed class DbWorkflowAlertNotifier(
    IDbContextFactory<MediaDbContext> dbFactory,
    ILogger<DbWorkflowAlertNotifier> logger)
    : IWorkflowAlertNotifier
{
    public Task NotifyFailedAsync(
        Guid workflowId,
        string workflowName,
        string errorMessage,
        CancellationToken cancellationToken = default)
        => WriteAsync(workflowId, workflowName, "Failed", $"{workflowName} failed: {errorMessage}", cancellationToken);

    public Task NotifyCompletedAsync(
        Guid workflowId,
        string workflowName,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
        => WriteAsync(workflowId, workflowName, "Completed", $"{workflowName} completed in {duration:hh\\:mm\\:ss}", cancellationToken);

    private async Task WriteAsync(
        Guid workflowId,
        string workflowName,
        string kind,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.WorkflowAlerts.Add(new WorkflowAlertEntity
            {
                WorkflowId = workflowId.ToString(),
                WorkflowName = workflowName,
                Kind = kind,
                Message = message
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue workflow alert for {WorkflowId}", workflowId);
        }
    }
}
