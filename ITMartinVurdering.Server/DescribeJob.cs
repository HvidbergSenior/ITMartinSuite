using ITMartinVurdering.Application;

namespace ITMartinVurdering.Server;

/// <summary>Every 10 minutes: one round of Ting/Strik descriptions (at most one AI call). Vurdering__DescribeThings=false turns it off.</summary>
public sealed class DescribeJob(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<DescribeJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!cfg.GetValue("Vurdering:DescribeThings", true)) { log.LogInformation("Descriptions are off (Vurdering__DescribeThings=false)"); return; }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<DescribeUseCases>().RoundAsync(stop);
                if (sent > 0) log.LogInformation("Ting/Strik described: {Count}", sent);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception ex) { log.LogWarning("Describing Ting/Strik failed: {Message}", ex.Message); }
        }
        while (await timer.WaitForNextTickAsync(stop).AsTask().ContinueWith(t => !t.IsCanceled && t.Result));
    }
}
