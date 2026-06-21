using ITMartinBarTab.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBarTab.Server.Services;

public sealed class SessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SessionCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupExpiredAsync();
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CleanupExpiredAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarTabDbContext>();

        var expired = await db.Sessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow)
            .Include(s => s.Drinks)
            .ToListAsync();

        if (expired.Count == 0) return;

        foreach (var session in expired)
        {
            foreach (var drink in session.Drinks.Where(d => d.PhotoPath != null))
            {
                if (File.Exists(drink.PhotoPath))
                    File.Delete(drink.PhotoPath!);
            }
        }

        db.Sessions.RemoveRange(expired);
        await db.SaveChangesAsync();

        logger.LogInformation("Cleaned up {Count} expired sessions", expired.Count);
    }
}
