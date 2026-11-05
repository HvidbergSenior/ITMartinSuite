using ITMartinIndbakke.Domain;

namespace ITMartinIndbakke.Application;

/// <summary>Port: where the follow-ups (status + deadline + own note) are kept between restarts.</summary>
public interface IFollowUpStore
{
    IReadOnlyDictionary<string, FollowUp> All();
    void Save(FollowUp followUp);
    DateTimeOffset? Baseline { get; set; }
}

/// <summary>"Det haster nu" + the three presses: Læst (with an optional deadline), Klaret, and Fortryd.</summary>
public sealed class FollowUps(IFollowUpStore store, MailScanner scanner, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public IReadOnlyList<UrgentItem> Now() =>
        scanner.Last is { } o ? UrgentRules.Now(o.Important, store.All(), Today, baseline: store.Baseline) : [];

    /// <summary>"Venter på dit svar" (2026-11-05): people waiting for me - minus the ones I closed with Afsluttet (same Klaret mark).</summary>
    public IReadOnlyList<(MailHeader Mail, MailCategory Category)> Waiting() =>
        scanner.Last is { } o
            ? o.NeedsReply.Where(x => !(store.All().TryGetValue(x.Mail.MessageId, out var f) && f.Status == FollowUpStatus.Klaret)).ToList()
            : [];

    /// <summary>Closed with Afsluttet, newest first - so Fortryd can bring one back.</summary>
    public IReadOnlyList<(MailHeader Mail, MailCategory Category)> Closed() =>
        scanner.Last is { } o
            ? o.NeedsReply.Where(x => store.All().TryGetValue(x.Mail.MessageId, out var f) && f.Status == FollowUpStatus.Klaret).ToList()
            : [];

    public string? MarkRead(string messageId, DateOnly? deadline, string? note)
    {
        if (!Known(messageId)) return "Ukendt brev.";
        if (deadline is { } d && (d < Today.AddYears(-1) || d > Today.AddYears(2))) return "Datoen ser forkert ud.";
        store.Save(Get(messageId).Read(deadline, note));
        return null;
    }

    public string? MarkDone(string messageId)
    {
        if (!Known(messageId)) return "Ukendt brev.";
        store.Save(Get(messageId).Done());
        return null;
    }

    /// <summary>"Alt før i dag er klaret" - the base line (user 2026-11-04: "all is klaret. Do i have to push klaret on all?"
    /// + "a base button. Meaning all mails before this are handled"). One date, not one press per mail.</summary>
    public DateTimeOffset? Baseline => store.Baseline;

    public void SetBaseline() => store.Baseline = clock.GetUtcNow();

    public void ClearBaseline() => store.Baseline = null;

    public string? Reopen(string messageId)
    {
        if (!Known(messageId)) return "Ukendt brev.";
        store.Save(Get(messageId).Reopen());
        return null;
    }

    private FollowUp Get(string id) => store.All().TryGetValue(id, out var f) ? f : FollowUp.New(id);

    private bool Known(string id) =>
        !string.IsNullOrWhiteSpace(id) && (store.All().ContainsKey(id)
            || scanner.Last?.Important.Any(m => m.Mail.MessageId == id) == true
            || scanner.Last?.NeedsReply.Any(m => m.Mail.MessageId == id) == true);   // Afsluttet on "Venter på dit svar"
}
