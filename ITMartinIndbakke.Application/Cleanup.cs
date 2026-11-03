using ITMartinIndbakke.Domain;

namespace ITMartinIndbakke.Application;

/// <summary>Port: the mailbox changes v2 needs. Delete = Gmail's Papirkurv (30 days), never a real delete.</summary>
public interface IMailboxCleaner
{
    /// <summary>Move these inbox mails (by UID from the last scan) to Papirkurv. Returns how many were moved.</summary>
    Task<int> MoveToTrashAsync(IReadOnlyList<uint> uids, CancellationToken ct);

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

    public IReadOnlyList<CleanupAction> History() =>
        log.All().OrderByDescending(a => a.At).Take(30).ToList();

    public async Task<CleanupResult> UndoAsync(Guid id, CancellationToken ct)
    {
        var a = log.All().FirstOrDefault(x => x.Id == id);
        if (a is null) return new(false, "Ukendt oprydning.", 0, "", null);
        if (!a.CanUndo(clock.GetUtcNow())) return new(false, "Den kan ikke fortrydes længere.", 0, "", null);
        var back = await cleaner.RestoreAsync(a.MessageIds, ct);
        log.Save(a with { Restored = true });
        scanner.Start(scanner.Last?.Months ?? 12);   // the restored mails come back into the overview
        return new(true, $"{back} mails lagt tilbage i Indbakke." + (a.Unsubscribed.Length > 0 ? " Afmeldingen kan ikke fortrydes her – tilmeld dig igen hos afsenderen, hvis du vil." : ""), back, "", null);
    }
}
