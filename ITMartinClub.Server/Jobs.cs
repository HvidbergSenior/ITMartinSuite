using ITMartinClub.Application;

namespace ITMartinClub.Server;

// Every 15 min: messages that want confirmation, sent more than 24 h ago, get ONE automatic reminder per missing member.
public sealed class MessageReminderJob(IServiceScopeFactory scopes, ILogger<MessageReminderJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<TeamMessages>().SendDueRemindersAsync();
                if (sent > 0) log.LogInformation("Sent {N} message reminders", sent);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "Message reminder pass failed"); }
            await Task.Delay(TimeSpan.FromMinutes(15), ct);
        }
    }
}
