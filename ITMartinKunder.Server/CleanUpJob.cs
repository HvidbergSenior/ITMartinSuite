namespace ITMartinKunder.Server;

// Deletes what Retention says is too old, once a night (first run 5 minutes after start).
internal sealed class CleanUpJob(IServiceProvider services, ILogger<CleanUpJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), ct);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<Kundekartotek>().CleanUpAsync(ct);
                log.LogInformation("Clean-up: {Deleted} customers deleted (afmeldt > {Days} days / køb > {Years} years)", n,
                    Retention.UnsubscribedKeepDays, Retention.PurchaseKeepYears);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "Clean-up failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
