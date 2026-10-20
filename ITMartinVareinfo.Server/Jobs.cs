using ITMartinVareinfo.Application;

namespace ITMartinVareinfo.Server;

// Makes the uploads ready (reading copy + close-ups), a few side by side: 100 phones can send faster than the photos can
// be cut, so they wait here instead of on the phones. Vareinfo__PhotoWorkers sets how many at once (default half the
// cores - the photoserver also runs the other apps).
public sealed class PhotoJob(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<PhotoJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var workers = Math.Clamp(cfg.GetValue("Vareinfo:PhotoWorkers", Environment.ProcessorCount / 2), 1, 16);
        log.LogInformation("Photo job: {Workers} at a time", workers);
        while (!stop.IsCancellationRequested)
        {
            var done = 0;
            try
            {
                using var scope = scopes.CreateScope();
                done = await scope.ServiceProvider.GetRequiredService<CaptureUseCases>().ProcessWaitingAsync(workers * 4, workers, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception ex) { log.LogWarning("Photo round failed: {Message}", ex.Message); }
            // Work waiting: straight on. Nothing: look again in 2 seconds (one small query).
            if (done == 0)
                try { await Task.Delay(TimeSpan.FromSeconds(2), stop); } catch (OperationCanceledException) { return; }
        }
    }
}
