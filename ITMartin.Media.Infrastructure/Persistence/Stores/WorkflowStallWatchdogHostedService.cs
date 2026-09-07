using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

/// <summary>
/// Catches the failure mode a "Failed" alert can't: a workflow that's still
/// technically "Running" but has stopped making progress (a hung step, a
/// lock nothing releases, ...). Nothing else notices this - WorkflowExecutor
/// only calls IWorkflowAlertNotifier on an actual exception, and a genuine
/// hang throws nothing. Found 2026-09-07 when a QuickSort run sat stuck on
/// Manifest1BuildWorkflowStep for ~10 hours with zero signal until someone
/// asked "it has run whole day".
/// </summary>
public sealed class WorkflowStallWatchdogHostedService(
    IDbContextFactory<MediaDbContext> dbFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowStallWatchdogHostedService> logger)
    : BackgroundService
{
    public static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Workflow stall check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Public (not private) so the per-tick logic is directly unit-testable
    // without waiting out a real PeriodicTimer - matches this codebase's
    // convention for other pure/testable pieces (QuickSortManifestBuilder,
    // LedgerQaService.BuildDigest, ...).
    public async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = DateTime.UtcNow - StallThreshold;

        var stalled = await db.WorkflowInstances
            .Where(x => x.Status == "Running" && x.UpdatedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (stalled.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var alertNotifier = scope.ServiceProvider.GetRequiredService<IWorkflowAlertNotifier>();

        foreach (var workflow in stalled)
        {
            // One alert per stall, not one every 5 minutes forever - re-check
            // against alerts already on file for this workflow rather than
            // tracking in-memory state that a restart would forget anyway.
            var alreadyAlerted = await db.WorkflowAlerts.AnyAsync(
                a => a.WorkflowId == workflow.WorkflowId.ToString() && a.Kind == "Stalled",
                cancellationToken);

            if (alreadyAlerted)
            {
                continue;
            }

            await alertNotifier.NotifyStalledAsync(
                workflow.WorkflowId,
                workflow.WorkflowName,
                workflow.CurrentStep ?? "unknown",
                DateTime.UtcNow - workflow.UpdatedAtUtc,
                cancellationToken);
        }
    }
}
