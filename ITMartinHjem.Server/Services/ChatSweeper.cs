using ITMartinHjem.Application;

namespace ITMartinHjem.Server.Services;

/// <summary>Every 15 s: sends what ChatUseCases finds due in the database - the visitor's "Martin har svaret dig" and
/// Martin's own "venter på svar" copy. Because the state is in the database, a restart or deploy loses nothing.</summary>
public sealed class ChatSweeper(ChatUseCases chat, ILogger<ChatSweeper> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try { await chat.SweepAsync(DateTime.UtcNow); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "Chat sweep failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
