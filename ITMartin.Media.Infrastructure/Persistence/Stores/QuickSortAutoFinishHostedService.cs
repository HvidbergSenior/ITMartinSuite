using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

/// <summary>
/// Closes the gap that made every ToshibaTest delivery a manual, Claude-
/// driven chain (2026-09-08): finish-library, push-to-nas, and wire-gallery
/// used to be three separate debug endpoints nothing ever called
/// automatically once QuickSort itself finished. This watches for a
/// QuickSortWorkflow reaching "Completed" and runs the rest of the chain
/// itself - the sort was always autonomous, this makes the delivery
/// autonomous too. Scoped to whichever single client this process is
/// configured for (MediaSettings:ClientSlug/LibraryRoot) - each client gets
/// its own filesorter-web/worker pair, so there's no cross-client fan-out to
/// do here.
/// </summary>
public sealed class QuickSortAutoFinishHostedService(
    IDbContextFactory<MediaDbContext> dbFactory,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<QuickSortAutoFinishHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    // A failed chain writes no marker, so without this the next poll simply
    // tries again - and each attempt re-runs a full tar+scp of the whole
    // library to the NAS. A persistent failure (bad credentials, gallery
    // container down, disk full) would push hundreds of GB every 2 minutes
    // for as long as the process lives. Give up after this many consecutive
    // failures for one workflow and wait for a human instead; delivery is
    // never so urgent that it is worth saturating the network unattended.
    private const int MaxDeliveryAttempts = 3;

    private Guid _failingWorkflowId;
    private int _consecutiveFailures;

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
                logger.LogWarning(ex, "QuickSort auto-finish check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Every per-folder "thumbnails" directory under the library, except the
    // offline gallery's own _Galleri/thumbs, which the delivered index.html
    // depends on. Public so the delivery behaviour is directly testable.
    public static int RemovePerFolderThumbnails(string libraryRoot, ILogger logger)
    {
        if (!Directory.Exists(libraryRoot)) return 0;

        var galleryRoot = Path.Combine(libraryRoot, "_Galleri");
        var removed = 0;

        foreach (var dir in Directory.EnumerateDirectories(libraryRoot, "thumbnails", SearchOption.AllDirectories))
        {
            if (dir.StartsWith(galleryRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                Directory.Delete(dir, recursive: true);
                removed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not remove per-folder thumbnails at {Path}", dir);
            }
        }

        logger.LogInformation(
            "Removed {Count} per-folder thumbnails directories from {LibraryRoot} (the NAS copy keeps them; the offline gallery uses _Galleri/thumbs)",
            removed,
            libraryRoot);

        return removed;
    }

    // Public (not private) so this is directly unit-testable without a real
    // PeriodicTimer - same convention as WorkflowStallWatchdogHostedService.
    public async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        var libraryRoot = configuration["MediaSettings:LibraryRoot"];
        var clientSlug = configuration["MediaSettings:ClientSlug"];
        if (string.IsNullOrWhiteSpace(libraryRoot) || string.IsNullOrWhiteSpace(clientSlug))
        {
            // No per-client config on this process (e.g. a local/dev run) -
            // nothing to auto-deliver against.
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var latest = await db.WorkflowInstances
            .Where(x => x.WorkflowName == "QuickSortWorkflow")
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null || latest.Status != "Completed")
        {
            return;
        }

        // One delivery per completed run, tracked with a marker file (same
        // filesystem-marker convention as QuickSortBaselineHelper) rather
        // than a DB column - survives a DB reset/recreate, which is exactly
        // the kind of event this run's own troubleshooting produced.
        var markerPath = Path.Combine(libraryRoot, ".autofinished");
        if (File.Exists(markerPath))
        {
            var alreadyDone = await File.ReadAllTextAsync(markerPath, cancellationToken);
            if (alreadyDone.Trim() == latest.WorkflowId.ToString())
            {
                return;
            }
        }

        if (_failingWorkflowId == latest.WorkflowId &&
            _consecutiveFailures >= MaxDeliveryAttempts)
        {
            return;
        }

        logger.LogInformation(
            "QuickSort run {WorkflowId} completed - auto-finishing {LibraryRoot}",
            latest.WorkflowId,
            libraryRoot);

        using var scope = scopeFactory.CreateScope();
        var finishing = scope.ServiceProvider.GetRequiredService<ILibraryFinishingService>();
        var nasDelivery = scope.ServiceProvider.GetRequiredService<INasDeliveryService>();
        var alertNotifier = scope.ServiceProvider.GetRequiredService<IWorkflowAlertNotifier>();

        // "password should just be the name of the folder and the year"
        // (2026-09-08) - no separate secret to configure per client, and
        // changes automatically each year rather than staying static
        // forever.
        var slug = clientSlug.ToLowerInvariant();
        var password = $"{clientSlug}{DateTime.UtcNow.Year}";

        try
        {
            await finishing.RunAsync(libraryRoot, cancellationToken);

            var pushResult = await nasDelivery.PushToNasAsync(libraryRoot, slug, cancellationToken);
            if (!pushResult.Success)
            {
                throw new InvalidOperationException($"push-to-nas failed: {pushResult.Error}");
            }

            var wireResult = await nasDelivery.WireGalleryAsync(slug, clientSlug, password, cancellationToken);
            if (!wireResult.Success)
            {
                throw new InvalidOperationException($"wire-gallery failed: {wireResult.Error}");
            }

            // Deliberately AFTER push-to-nas, never before: the two thumbnail
            // sets serve different consumers. gallery-web's live /api/browse
            // reads a per-folder "thumbnails" subfolder next to each file and
            // falls back to full-resolution originals when it is missing, so
            // the copy pushed to the NAS must keep them. The physical drive
            // handed to the customer uses the offline gallery instead, which
            // reads the centralized _Galleri/thumbs - it never touches these,
            // so on the drive they are just a "thumbnails" folder inside every
            // single year folder the customer opens. Measured on ToshibaTest:
            // 17,985 files the delivered gallery has zero references to.
            RemovePerFolderThumbnails(libraryRoot, logger);

            await File.WriteAllTextAsync(markerPath, latest.WorkflowId.ToString(), cancellationToken);

            _consecutiveFailures = 0;

            await alertNotifier.NotifyDeliveredAsync(latest.WorkflowId, latest.WorkflowName, slug, cancellationToken);
        }
        catch (Exception ex)
        {
            if (_failingWorkflowId != latest.WorkflowId)
            {
                _failingWorkflowId = latest.WorkflowId;
                _consecutiveFailures = 0;
            }

            _consecutiveFailures++;

            var givingUp = _consecutiveFailures >= MaxDeliveryAttempts;

            logger.LogError(
                ex,
                "Auto-finish chain failed for {WorkflowId} (attempt {Attempt} of {MaxAttempts}){Suffix}",
                latest.WorkflowId,
                _consecutiveFailures,
                MaxDeliveryAttempts,
                givingUp
                    ? " - giving up, will not retry until this process restarts"
                    : string.Empty);

            await alertNotifier.NotifyFailedAsync(
                latest.WorkflowId,
                latest.WorkflowName,
                $"Auto-finish (finish-library/push-to-nas/wire-gallery) failed on attempt {_consecutiveFailures}/{MaxDeliveryAttempts}: {ex.Message}",
                cancellationToken);
        }
    }
}
