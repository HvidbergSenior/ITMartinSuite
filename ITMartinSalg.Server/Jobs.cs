using ITMartinSalg.Application;

namespace ITMartinSalg.Server;

// Køb nu orders: asks Stripe every minute whether a pending one was paid (Lager is told, the copies leave the stock) or
// expired (the copies are free again), and sends a paid sale again when Lager did not answer the first time. The
// buyer's return page asks too - this catches the ones who closed the tab.
public sealed class OrderJob(IServiceScopeFactory scopes, ILogger<OrderJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var changed = await scope.ServiceProvider.GetRequiredService<ShopUseCases>().SyncAsync(ct);
                if (changed > 0) log.LogInformation("Shop orders: {Changed} paid, booked or expired", changed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning("Shop sync failed: {Message}", ex.Message); }
        }
    }
}
