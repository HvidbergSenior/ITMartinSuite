using ITMartinVareinfo.Application;

namespace ITMartinVareinfo.Server;

// Reads waiting kolofon photos, up to 10 in one AI call, every 2 minutes, and sends what was read to Lager.
// Vareinfo__KolofonReader=false turns it off.
public sealed class KolofonJob(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<KolofonJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!cfg.GetValue("Vareinfo:KolofonReader", true)) { log.LogInformation("Kolofon reader is off"); return; }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var read = await scope.ServiceProvider.GetRequiredService<KolofonUseCases>().ReadWaitingAsync(stop);
                if (read > 0) log.LogInformation("Kolofons read: {Count}", read);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { log.LogWarning("Kolofon round failed: {Message}", ex.Message); }   // cap reached, Lager down - try again later
        }
        while (await WaitAsync(timer, stop));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer t, CancellationToken stop)
    {
        try { return await t.WaitForNextTickAsync(stop); } catch (OperationCanceledException) { return false; }
    }
}
