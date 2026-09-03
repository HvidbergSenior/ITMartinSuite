using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFileSorter.Server.Services;

/// <summary>
/// Turns WorkflowAlerts rows (written by DbWorkflowAlertNotifier, possibly
/// from the separate Worker process) into an actual Web Push notification.
/// Lives in Server because that's where the VAPID keys and browser
/// subscriptions already are - see DbWorkflowAlertNotifier's doc comment
/// for why this is DB-polled instead of a direct cross-process call.
/// </summary>
public sealed class WorkflowAlertPushHostedService(
    IDbContextFactory<MediaDbContext> dbFactory,
    FileSorterPushService pushService,
    ILogger<WorkflowAlertPushHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Workflow alert push poll failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var pending = await db.WorkflowAlerts
            .Where(a => a.SentAtUtc == null)
            .OrderBy(a => a.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        foreach (var alert in pending)
        {
            var title = alert.Kind == "Failed"
                ? $"FileSorter: {alert.WorkflowName} failed"
                : $"FileSorter: {alert.WorkflowName} completed";

            await pushService.SendToAllAsync(title, alert.Message);
            alert.SentAtUtc = DateTime.UtcNow;
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
