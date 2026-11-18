using ITMartinIndbakke.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinIndbakke.Application;

/// <summary>What the mailbox gives back: the inbox headers and who the user has written to (from Sendt post).</summary>
public sealed record MailboxScan(IReadOnlyList<MailHeader> Inbox, ISet<string> WroteTo, ISet<string> Mine)
{
    /// <summary>The newest mail I sent per Gmail conversation (Sendt post, headers only) - "Venter på svar". Empty for Outlook.</summary>
    public IReadOnlyList<SentMail> Sent { get; init; } = [];
}

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
    private MailboxScan? _scan;
    private int _months = 12;

    public MailOverview? Last { get; private set; }

    /// <summary>The raw scan behind <see cref="Last"/> (headers + Sendt post) - the secretary works on it after each scan.</summary>
    public MailboxScan? LastScan { get { lock (_gate) return _scan; } }

    /// <summary>Runs after every good scan (Martin's own mailbox: dates + answers -> Husk). A failure here never breaks the scan.</summary>
    public Func<MailboxScan, CancellationToken, Task>? AfterScan { get; set; }
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

    /// <summary>Mails moved to Papirkurv leave the overview at once - no new scan needed.</summary>
    public void Forget(ISet<string> messageIds)
    {
        lock (_gate)
        {
            if (_scan is null) return;
            _scan = _scan with { Inbox = _scan.Inbox.Where(m => !messageIds.Contains(m.MessageId)).ToList() };
            Last = MailRules.Build(_scan.Inbox, _scan.WroteTo, _months, clock.GetUtcNow(), _scan.Mine);
        }
    }

    private async Task RunAsync(int months)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            var progress = new Progress<(int Done, int Total)>(p => { Done = p.Done; Total = p.Total; });
            var scan = await mailbox.ScanAsync(months, progress, cts.Token);
            lock (_gate) { _scan = scan; _months = months; Last = MailRules.Build(scan.Inbox, scan.WroteTo, months, clock.GetUtcNow(), scan.Mine); }
            log.LogInformation("Indbakke scan: {Count} mails, {Senders} senders", Last.Total, Last.Senders.Count);
            if (AfterScan is { } after)
            {
                try { await after(scan, cts.Token); }
                catch (Exception e) { log.LogWarning(e, "Indbakke secretary after scan failed"); }
            }
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

/// <summary>Martin's own Indbakke - the workspace behind his PIN - and his secretary (dates + "Venter på svar" -> Husk).
/// Min Indbakke guests never get a secretary: their mail text is never read.</summary>
public sealed record OwnerMail(MailWorkspace Workspace, Secretary Secretary);

public static class IndbakkeApplication
{
    public static IServiceCollection AddIndbakkeApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        // Martin's own mailbox (Indbakke__User/AppPassword) = one workspace for the whole life of the app.
        services.AddSingleton(sp =>
        {
            var clock = sp.GetRequiredService<TimeProvider>();
            var scanner = new MailScanner(sp.GetRequiredService<IMailbox>(), sp.GetRequiredService<ILogger<MailScanner>>(), clock);
            var secretary = new Secretary(scanner, sp.GetRequiredService<IMailText>(), sp.GetRequiredService<ISecretaryStore>(),
                sp.GetRequiredService<IHusk>(), clock, sp.GetRequiredService<ILogger<Secretary>>());
            scanner.AfterScan = secretary.AfterScanAsync;
            return new OwnerMail(new MailWorkspace(scanner,
                new FollowUps(sp.GetRequiredService<IFollowUpStore>(), scanner, clock),
                new Cleanup(scanner, sp.GetRequiredService<IMailboxCleaner>(), sp.GetRequiredService<IUnsubscriber>(), sp.GetRequiredService<ICleanupLog>(), clock),
                "Martin"), secretary);
        });
        services.AddSingleton<MailSessions>();   // Min Indbakke: one workspace per login, key in memory only
        return services;
    }
}
