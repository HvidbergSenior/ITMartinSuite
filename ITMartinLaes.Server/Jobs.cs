using ITMartinLaes.Application;

namespace ITMartinLaes.Server;

// The summary is made in the background, one AI call every 30 seconds at most (a part of ~20 pages, then the summary
// itself), so a 288-page book is ready about 8-10 minutes after it is put on the shelf. Summaries stops at the day's cap.
public sealed class SummaryJob(IServiceScopeFactory scopes, ILogger<SummaryJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<Summaries>().WorkAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning("Summary job failed: {Message}", ex.Message); }
        }
    }
}
