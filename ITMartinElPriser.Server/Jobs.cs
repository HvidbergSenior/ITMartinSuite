using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Server;

// Every 5 minutes: the messages due an hour before the cheapest (and dearest) two hours. The job only sets the pace -
// what is due and who gets it is in NotifyUseCases/PushPlan.
public sealed class NotificationJob(NotifyUseCases notify, ILogger<NotificationJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var sent = await notify.RunOnceAsync(DkTime.Now, ct);
                if (sent > 0) log.LogInformation("Sent {Count} price message(s)", sent);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { log.LogError(ex, "Notification tick failed"); }
            try { await Task.Delay(PushPlan.Tick, ct); } catch (OperationCanceledException) { return; }
        }
    }
}
