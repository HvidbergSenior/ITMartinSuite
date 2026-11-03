using ITMartinIndbakke.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinIndbakke.Application;

/// <summary>What the mailbox gives back: the inbox headers and who the user has written to (from Sendt post).</summary>
public sealed record MailboxScan(IReadOnlyList<MailHeader> Inbox, ISet<string> WroteTo);

/// <summary>The port to the mailbox. v1 is read-only: it never moves, flags or deletes anything.</summary>
public interface IMailbox
{
    bool IsConfigured { get; }
    Task<MailboxScan> ScanAsync(int months, IProgress<(int Done, int Total)> progress, CancellationToken ct);
}

/// <summary>
/// One scan at a time, in the background; the page asks for the progress and then the overview.
/// The overview is kept in memory only (headers, never mail text) - a restart means "scan again".
/// </summary>
public sealed class MailScanner(IMailbox mailbox, ILogger<MailScanner> log, TimeProvider clock)
{
    private readonly object _gate = new();
    private Task? _running;

    public MailOverview? Last { get; private set; }
    public bool Running => _running is { IsCompleted: false };
    public int Done { get; private set; }
    public int Total { get; private set; }
    public string? Error { get; private set; }
    public bool IsConfigured => mailbox.IsConfigured;

    /// <summary>Start a scan unless one runs already. Returns false when one was running.</summary>
    public bool Start(int months)
    {
        months = Math.Clamp(months, 1, 36);
        lock (_gate)
        {
            if (Running) return false;
            Done = 0; Total = 0; Error = null;
            _running = Task.Run(() => RunAsync(months));
            return true;
        }
    }

    private async Task RunAsync(int months)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            var progress = new Progress<(int Done, int Total)>(p => { Done = p.Done; Total = p.Total; });
            var scan = await mailbox.ScanAsync(months, progress, cts.Token);
            Last = MailRules.Build(scan.Inbox, scan.WroteTo, months, clock.GetUtcNow());
            log.LogInformation("Indbakke scan: {Count} mails, {Senders} senders", Last.Total, Last.Senders.Count);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Indbakke scan failed");
            Error = e.Message.Contains("AUTHENTICATIONFAILED", StringComparison.OrdinalIgnoreCase) || e.GetType().Name.Contains("Authentication")
                ? "Gmail sagde nej til adgangskoden. Tjek app-adgangskoden."
                : "Scanningen stoppede: " + e.Message;
        }
    }
}

public static class IndbakkeApplication
{
    public static IServiceCollection AddIndbakkeApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MailScanner>();
        services.AddSingleton<FollowUps>();
        return services;
    }
}
