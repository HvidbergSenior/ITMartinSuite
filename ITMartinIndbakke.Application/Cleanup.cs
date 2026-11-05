using ITMartinIndbakke.Domain;

namespace ITMartinIndbakke.Application;

/// <summary>Port: the mailbox changes v2 needs. Delete = Gmail's Papirkurv (30 days), never a real delete.</summary>
public interface IMailboxCleaner
{
    /// <summary>"Ryd alt det gamle" needs Gmail's labels for Fortryd - Outlook says false (2026-11-05).</summary>
    bool CanClearOld => true;

    /// <summary>Move these inbox mails (by UID from the last scan) to Papirkurv. Returns how many were moved.</summary>
    Task<int> MoveToTrashAsync(IReadOnlyList<uint> uids, CancellationToken ct);

    /// <summary>Count the old pile: (all before the date, the part that would go to Papirkurv, starred).</summary>
    Task<(int Total, int Trash, int Starred)> CountOldAsync(DateOnly before, OldMode mode, CancellationToken ct);

    /// <summary>Label the old pile, then the trash part -> Papirkurv, the rest (SletReklame) -> Arkiv.</summary>
    Task<(int Trashed, int Archived)> ClearOldAsync(DateOnly before, OldMode mode, string label, CancellationToken ct);

    /// <summary>Fortryd for the old pile: everything with the label back into Indbakke (from Papirkurv and Arkiv).</summary>
    Task<int> RestoreLabelAsync(string label, CancellationToken ct);

    /// <summary>Move mails back from Papirkurv to Indbakke, found by Message-ID. Returns how many came back.</summary>
    Task<int> RestoreAsync(IReadOnlyList<string> messageIds, CancellationToken ct);
}

/// <summary>Port: unsubscribe - RFC 8058 one-click POST, or a mail to the list's mailto address.</summary>
public interface IUnsubscriber
{
    /// <summary>True when the sender confirmed (2xx) or the mail was sent.</summary>
    Task<bool> UnsubscribeAsync(UnsubscribeTarget target, CancellationToken ct);
}

/// <summary>Port: the log of presses, so Fortryd works after a restart.</summary>
public interface ICleanupLog
{
    IReadOnlyList<CleanupAction> All();
    void Save(CleanupAction action);
}

public sealed record CleanupResult(bool Ok, string Message, int Moved, string Unsubscribed, string? OpenPage);

/// <summary>"Ryd op" per sender: preview, then Afmeld and/or Slet (to Papirkurv), and Fortryd.</summary>
public sealed class Cleanup(MailScanner scanner, IMailboxCleaner cleaner, IUnsubscriber unsubscriber, ICleanupLog log, TimeProvider clock)
{
    public bool CanClearOld => cleaner.CanClearOld;

    public CleanupPreview? Preview(string address) =>
        scanner.Last is { } o && !string.IsNullOrWhiteSpace(address) ? CleanupRules.Preview(address.Trim(), o.All) : null;

    /// <param name="unsubscribe">press "Afmeld"</param>
    /// <param name="delete">press "Slet"</param>
    public async Task<CleanupResult> RunAsync(string address, bool unsubscribe, bool delete, CancellationToken ct)
    {
        var p = Preview(address);
        if (p is null) return new(false, "Scan din mail først.", 0, "", null);

        // 1. Unsubscribe first - then no new ones arrive while the old ones go to Papirkurv.
        var unsub = "";
        string? openPage = null;
        if (unsubscribe)
        {
            if (p.Unsubscribe is not { } t) unsub = "ingen afmelding tilbudt";
            else if (t.Kind == UnsubscribeKind.Page) { unsub = "åbn siden for at afmelde"; openPage = t.Https!.ToString(); }
            else if (await unsubscriber.UnsubscribeAsync(t, ct))
                unsub = t.Kind == UnsubscribeKind.OneClick ? "afmeldt" : "afmeldings-mail sendt";
            else if (t.Https is not null) { unsub = "afmelding svarede ikke – åbn siden"; openPage = t.Https.ToString(); }
            else unsub = "afmelding mislykkedes";
        }

        // 2. Delete = Papirkurv, only the mails the rules allow.
        var moved = 0;
        if (delete && p.ToDelete.Count > 0)
        {
            moved = await cleaner.MoveToTrashAsync(p.ToDelete.Select(m => m.Uid).ToList(), ct);
            scanner.Forget(p.ToDelete.Select(m => m.MessageId).ToHashSet());
        }

        if (moved > 0 || unsub.Length > 0)
            log.Save(new CleanupAction(Guid.NewGuid(), clock.GetUtcNow(), p.Address, p.Name,
                moved > 0 ? p.ToDelete.Select(m => m.MessageId).ToList() : [], unsub, false));

        var text = string.Join(" · ", new[]
        {
            moved > 0 ? $"{moved} mails lagt i papirkurven (kan fortrydes i 30 dage)" : delete ? "Ingen mails at slette" : "",
            unsub.Length > 0 ? char.ToUpper(unsub[0]) + unsub[1..] : "",
        }.Where(s => s.Length > 0));
        return new(true, text, moved, unsub, openPage);
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<OldPreview?> PreviewOldAsync(DateOnly before, OldMode mode, CancellationToken ct)
    {
        if (!OldCleanupRules.IsValid(before, Today)) return null;
        var (total, trash, starred) = await cleaner.CountOldAsync(before, mode, ct);
        return new OldPreview(before, total, trash, mode == OldMode.SletAlt ? 0 : Math.Max(0, total - trash - starred), starred);
    }

    // Big jobs (21,000 mails take minutes) run in the background on the server: Cloudflare cuts a request after 100 s,
    // and a cut request must never stop a move halfway. The page asks /status until it is done.
    private readonly object _jobGate = new();
    private Task? _job;
    public bool JobRunning => _job is { IsCompleted: false };
    public string? JobMessage { get; private set; }

    private bool StartJob(string working, Func<Task<string>> work)
    {
        lock (_jobGate)
        {
            if (JobRunning) return false;
            JobMessage = working;
            _job = Task.Run(async () =>
            {
                try { JobMessage = await work(); }
                catch (Exception e) { JobMessage = "Det stoppede undervejs: " + e.Message + " Det, der nåede at blive flyttet, kan du fortryde nedenfor."; }
            });
            return true;
        }
    }

    /// <summary>Starts "Ryd alt det gamle". The log entry (with the label) is saved FIRST, so Fortryd works even if it stops halfway.</summary>
    public string? StartClearOld(DateOnly before, OldMode mode)
    {
        if (!OldCleanupRules.IsValid(before, Today)) return "Datoen ser forkert ud.";
        var now = clock.GetUtcNow();
        var label = OldCleanupRules.Label(now);
        var entry = new CleanupAction(Guid.NewGuid(), now, "", "Alt før " + before.ToString("d. MMM yyyy", new System.Globalization.CultureInfo("da-DK")),
            [], "", false, label);
        var started = StartJob("Rydder op … det kan tage nogle minutter. Du kan godt lukke siden.", async () =>
        {
            log.Save(entry);
            var (trashed, archived) = await cleaner.ClearOldAsync(before, mode, label, CancellationToken.None);
            log.Save(entry with { Trashed = trashed, Archived = archived });
            scanner.Start(scanner.Last?.Months ?? 12);
            if (trashed + archived == 0) return "Der var ingen mails at rydde.";
            return trashed + " mails i papirkurven (kan fortrydes i 30 dage)" + (archived > 0 ? " · " + archived + " lagt i arkivet" : "")
                + ". I Gmail har de etiketten " + label + ".";
        });
        return started ? null : "En oprydning kører allerede.";
    }

    public IReadOnlyList<CleanupAction> History() =>
        log.All().OrderByDescending(a => a.At).Take(30).ToList();

    /// <summary>Fortryd - in the background too (a whole old pile is thousands of mails).</summary>
    public string? StartUndo(Guid id)
    {
        var a = log.All().FirstOrDefault(x => x.Id == id);
        if (a is null) return "Ukendt oprydning.";
        if (!a.CanUndo(clock.GetUtcNow())) return "Den kan ikke fortrydes længere.";
        var started = StartJob("Lægger mails tilbage … det kan tage nogle minutter.", async () =>
        {
            var back = a.Label is { } label
                ? await cleaner.RestoreLabelAsync(label, CancellationToken.None)
                : await cleaner.RestoreAsync(a.MessageIds, CancellationToken.None);
            log.Save(a with { Restored = true });
            scanner.Start(scanner.Last?.Months ?? 12);   // the restored mails come back into the overview
            return back + " mails lagt tilbage i Indbakke." + (a.Unsubscribed.Length > 0 ? " Afmeldingen kan ikke fortrydes her – tilmeld dig igen hos afsenderen, hvis du vil." : "");
        });
        return started ? null : "En oprydning kører allerede – vent til den er færdig.";
    }
}
