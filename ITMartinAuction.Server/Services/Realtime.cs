using ITMartinAuction.Application;
using ITMartinAuction.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ITMartinAuction.Server.Services;

// The ports that need SignalR live here in the Server: telling every phone in an auction, and the hammer countdown.
public sealed class SignalRBroadcast(IHubContext<AuctionHub> hub) : IAuctionBroadcast
{
    public Task SendAsync(string code, string message, object? payload = null) =>
        payload is null ? hub.Clients.Group(code).SendAsync(message) : hub.Clients.Group(code).SendAsync(message, payload);
}

// Deletes auctions 7 days after they were created, every 6 hours.
public sealed class CleanupJob(IServiceScopeFactory scopes, ILogger<CleanupJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromHours(6), ct); } catch (OperationCanceledException) { return; }
            try
            {
                using var scope = scopes.CreateScope();
                var removed = await scope.ServiceProvider.GetRequiredService<CleanupUseCases>().RemoveExpiredAsync(ct);
                if (removed > 0) log.LogInformation("Removed {Count} expired auctions", removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning("Cleanup failed: {Message}", ex.Message); }
        }
    }
}
