using ITMartinLager.Application;

namespace ITMartinLager.Server;

// The background jobs. Each one only sets the pace and calls a use case - the work itself is in Application.

// Works through the register: items never priced first, then the ones priced more than 30 days ago. A few items a
// minute - every site gets a handful of searches a minute, never a flood. Off with Lager__PriceHunter=false;
// Lager__PriceHuntPerMinute sets the pace (default 4 = ~5,700 items a day).
public sealed class PriceHunterJob(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<PriceHunterJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!cfg.GetValue("Lager:PriceHunter", true)) { log.LogInformation("Price hunter is off (Lager__PriceHunter=false)"); return; }
        var perMinute = Math.Clamp(cfg.GetValue("Lager:PriceHuntPerMinute", 4), 1, 30);
        try { await Task.Delay(TimeSpan.FromSeconds(30), stop); } catch (OperationCanceledException) { return; }

        while (!stop.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            try
            {
                using var scope = scopes.CreateScope();
                var hunt = scope.ServiceProvider.GetRequiredService<PriceHuntUseCases>();
                var ids = await hunt.NextAsync(perMinute, stop);
                foreach (var id in ids)
                    log.LogInformation("Priced item {Id}: {Ok} sources answered", id, await hunt.HuntAsync(id, stop));
                if (ids.Count == 0) { await Task.Delay(TimeSpan.FromMinutes(10), stop); continue; }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { log.LogWarning(ex, "Price hunter round failed"); }

            var wait = TimeSpan.FromMinutes(1) - (DateTime.UtcNow - started);
            if (wait > TimeSpan.Zero)
                try { await Task.Delay(wait, stop); } catch (OperationCanceledException) { return; }
        }
    }
}

// Every 2 minutes: fetch results for items that are up for auction, so a sale is in the register long before
// Live Auktion cleans the auction away.
public sealed class AuctionSyncJob(IServiceScopeFactory scopes, ILogger<AuctionSyncJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var changed = await scope.ServiceProvider.GetRequiredService<AuctionUseCases>().SyncAsync(ct);
                if (changed > 0) log.LogInformation("Auction sync: {Changed} items updated", changed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Auction sync failed"); }
        }
    }
}
