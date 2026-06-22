using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAdhd.Server.Services;

public sealed class ItemCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ItemCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync();
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CleanupAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdhdDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var stale = await db.StoredItems
            .Where(x => x.UpdatedAt < cutoff)
            .ToListAsync();

        if (stale.Count == 0) return;

        db.StoredItems.RemoveRange(stale);
        await db.SaveChangesAsync();

        logger.LogInformation("ADHD cleanup: removed {Count} stale items (older than 30 days)", stale.Count);
    }
}
