using ITMartinIndkoeb.Application;

namespace ITMartinIndkoeb.Server;

/// <summary>Reads waiting pictures every 20 s, one at a time (Kvittering's OCR is the slow part).</summary>
public sealed class BilledJob(IServiceProvider sp, ILogger<BilledJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<Billeder>().LaesVentendeAsync(stop);
                if (n > 0) log.LogInformation("Læste {N} billeder", n);
            }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogWarning(e, "Billedjob fejlede"); }
            try { await Task.Delay(TimeSpan.FromSeconds(20), stop); } catch (OperationCanceledException) { }
        }
    }
}
