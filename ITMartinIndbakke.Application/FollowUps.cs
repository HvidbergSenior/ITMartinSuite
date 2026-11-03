using ITMartinIndbakke.Domain;

namespace ITMartinIndbakke.Application;

/// <summary>Port: where the follow-ups (status + deadline + own note) are kept between restarts.</summary>
public interface IFollowUpStore
{
    IReadOnlyDictionary<string, FollowUp> All();
    void Save(FollowUp followUp);
}

/// <summary>"Det haster nu" + the three presses: Læst (with an optional deadline), Klaret, and Fortryd.</summary>
public sealed class FollowUps(IFollowUpStore store, MailScanner scanner, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public IReadOnlyList<UrgentItem> Now() =>
        scanner.Last is { } o ? UrgentRules.Now(o.Important, store.All(), Today) : [];

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

    public string? Reopen(string messageId)
    {
        if (!Known(messageId)) return "Ukendt brev.";
        store.Save(Get(messageId).Reopen());
        return null;
    }

    private FollowUp Get(string id) => store.All().TryGetValue(id, out var f) ? f : FollowUp.New(id);

    private bool Known(string id) =>
        !string.IsNullOrWhiteSpace(id) && (store.All().ContainsKey(id) || scanner.Last?.Important.Any(m => m.Mail.MessageId == id) == true);
}
