using ITMartinStarRealms.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStarRealms.Server.Services;

public sealed class CleanupService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(6), ct);
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StarRealmsDbContext>();
            var expired = await db.Sessions
                .Where(s => s.ExpiresAt < DateTime.UtcNow)
                .ToListAsync(ct);
            db.Sessions.RemoveRange(expired);
            await db.SaveChangesAsync(ct);
        }
    }
}
