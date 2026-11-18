using System.Globalization;
using ITMartinIndbakke.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinIndbakke.Application;

/// <summary>Port: the text of a few mails, read-only, in memory only. Key = the mail's UID in the inbox.</summary>
public interface IMailText
{
    Task<IReadOnlyDictionary<uint, string>> ReadTextsAsync(IReadOnlyList<uint> uids, CancellationToken ct);
}

/// <summary>A ball for Husk (husk.itmartin.dk /api/bolde, upsert by key).</summary>
public sealed record HuskBall(string Key, string Title, string Status, string NextStep, string WaitingOn = "", string Note = "",
    DateOnly? Deadline = null, string Area = "Mail");

/// <summary>Port: Husk's API. Returns null when it went fine, else a short Danish error.</summary>
public interface IHusk
{
    bool IsConfigured { get; }
    Task<string?> SaveAsync(HuskBall ball, CancellationToken ct);
}

/// <summary>A conversation the user asked Husk to watch ("Husk det") - or hid ("Ikke vigtig").</summary>
public sealed record Watched(ulong ThreadId, string Who, string Subject, DateTimeOffset Since, bool Hidden, DateTimeOffset? AnsweredAt = null, string? Key = null)
{
    /// <summary>The Husk ball this conversation updates - its own key when the ball was made by hand (fx "easypark-8539363").</summary>
    public string HuskKey => string.IsNullOrWhiteSpace(Key) ? "mail-venter-" + ThreadId : Key;
}

/// <summary>Port: /data/datoer.json (found dates + which mails were already read) and /data/venter.json (watched conversations).</summary>
public interface ISecretaryStore
{
    IReadOnlyList<MailDate> Dates();
    void SaveDate(MailDate date);
    bool WasChecked(string messageId);
    void MarkChecked(IEnumerable<string> messageIds);
    IReadOnlyList<Watched> Watched();
    void SaveWatched(Watched watched);
}

/// <summary>One line in "Du venter på svar".</summary>
public sealed record AwaitingItem(Awaiting Conversation, bool InHusk);

/// <summary>
/// The automatic secretary (user 2026-11-18). After each scan of Martin's own mailbox:
/// 1. reads the text of NEW mails from people/authorities (max <see cref="MaxTextsPerScan"/>, never newsletters), finds dates
///    ("samtale 26.11 kl. 9") and keeps only date + time + keyword + sender + subject;
/// 2. a watched conversation that got an answer -> its Husk ball goes back to "I gang" with "Læs svaret fra X".
/// Nothing goes to Husk before the user presses OK / Husk det (opt-in) - except the answer update on a ball he chose himself.
/// </summary>
public sealed class Secretary(MailScanner scanner, IMailText text, ISecretaryStore store, IHusk husk, TimeProvider clock, ILogger<Secretary> log)
{
    public const int MaxTextsPerScan = 100;
    public const int TextDays = 30;
    private static readonly CultureInfo Da = CultureInfo.GetCultureInfo("da-DK");

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public bool HuskReady => husk.IsConfigured;

    public async Task AfterScanAsync(MailboxScan scan, CancellationToken ct)
    {
        await FindDatesAsync(scan, ct);
        await UpdateAnswersAsync(scan, ct);
    }

    /// <summary>Which mails are worth reading for a date: from a person or an authority (not a list, not an ad, not myself), last 30 days.</summary>
    public static IReadOnlyList<MailHeader> Candidates(MailboxScan scan, Func<string, bool> wasChecked, DateTimeOffset now) =>
        scan.Inbox
            .Where(m => m.Date >= now.AddDays(-TextDays))
            .Where(m => !m.IsMailingList && !m.CanUnsubscribe && m.Tab != GmailTab.Promotions)
            .Where(m => !scan.Mine.Contains(m.FromAddress.ToLowerInvariant()))
            .Where(m => !wasChecked(m.MessageId))
            .OrderByDescending(m => m.Date)
            .Take(MaxTextsPerScan)
            .ToList();

    private async Task FindDatesAsync(MailboxScan scan, CancellationToken ct)
    {
        var todo = Candidates(scan, store.WasChecked, clock.GetUtcNow());
        if (todo.Count == 0) return;
        var texts = await text.ReadTextsAsync(todo.Select(m => m.Uid).ToList(), ct);
        var known = store.Dates().Select(d => d.Id).ToHashSet();
        var found = 0;
        foreach (var m in todo)
        {
            if (!texts.TryGetValue(m.Uid, out var body)) continue;
            foreach (var hit in DateFinder.Find(m.Subject + "\n" + body, Today))
            {
                var id = MailDate.MakeId(m.MessageId, hit.Date);
                if (!known.Add(id)) continue;
                store.SaveDate(new MailDate(id, m.MessageId, Name(m), m.Subject, hit.Date, hit.Time, hit.Word, DateStatus.Ny, clock.GetUtcNow()));
                found++;
            }
        }
        store.MarkChecked(todo.Select(m => m.MessageId));
        log.LogInformation("Indbakke secretary: read {Count} mails, {Found} new dates", todo.Count, found);
    }

    private async Task UpdateAnswersAsync(MailboxScan scan, CancellationToken ct)
    {
        foreach (var w in store.Watched().Where(w => !w.Hidden && w.AnsweredAt is null))
        {
            if (AwaitingRules.AnswerSince(w.ThreadId, w.Since, scan.Inbox, scan.Mine) is not { } answer) continue;
            var who = Name(answer);
            var error = await husk.SaveAsync(new HuskBall(w.HuskKey, $"Svar fra {who}: {Short(w.Subject)}", "i gang",
                $"Læs svaret fra {who} ({answer.Date.ToLocalTime().ToString("d.M", Da)}) og svar eller sæt en ny dato.", ""), ct);
            if (error is null) store.SaveWatched(w with { AnsweredAt = answer.Date });
            else log.LogWarning("Husk answer update failed: {Error}", error);
        }
    }

    /// <summary>"Datoer fundet i dine mails": new ones from today on, nearest first.</summary>
    public IReadOnlyList<MailDate> Dates() =>
        store.Dates().Where(d => d.Status == DateStatus.Ny && d.Date >= Today).OrderBy(d => d.Date).ThenBy(d => d.Time).ToList();

    /// <summary>OK (optionally with a corrected date/time) -> a ball in Husk with that date, so Husk reminds the day before + on the day.</summary>
    public async Task<string?> OkAsync(string id, DateOnly? date, TimeOnly? time, CancellationToken ct)
    {
        if (store.Dates().FirstOrDefault(d => d.Id == id) is not { } d) return "Ukendt dato.";
        if (date is { } nd && (nd < Today.AddDays(-1) || nd > Today.AddYears(2))) return "Datoen ser forkert ud.";
        if (!husk.IsConfigured) return "Husk er ikke sat op (Husk__ApiKey).";
        var fixedDate = d with { Date = date ?? d.Date, Time = date is null ? d.Time : time };
        var error = await husk.SaveAsync(new HuskBall(d.HuskKey, fixedDate.Title(), "i gang",
            $"Fra mail: {Short(d.Subject)}", "", $"Fundet i mail fra {d.From}.", fixedDate.Date), ct);
        if (error is not null) return error;
        store.SaveDate(fixedDate with { Status = DateStatus.Ok });
        return null;
    }

    public string? NotImportant(string id)
    {
        if (store.Dates().FirstOrDefault(d => d.Id == id) is not { } d) return "Ukendt dato.";
        store.SaveDate(d with { Status = DateStatus.IkkeVigtig });
        return null;
    }

    /// <summary>"Du venter på svar": I wrote last - hidden ones left out, the ones already in Husk marked.</summary>
    public IReadOnlyList<AwaitingItem> Awaiting()
    {
        if (scanner.LastScan is not { } scan) return [];
        var watched = store.Watched().ToDictionary(w => w.ThreadId);
        return AwaitingRules.Find(scan.Sent, scan.Inbox, scan.Mine, clock.GetUtcNow())
            .Where(a => !(watched.TryGetValue(a.ThreadId, out var w) && w.Hidden))
            .Select(a => new AwaitingItem(a, watched.ContainsKey(a.ThreadId)))
            .ToList();
    }

    /// <summary>"Husk det": a Husk ball "Venter på svar fra X" - it comes back as "Læs svaret" by itself when the answer arrives.</summary>
    public async Task<string?> WatchAsync(ulong threadId, CancellationToken ct)
    {
        if (Awaiting().FirstOrDefault(a => a.Conversation.ThreadId == threadId)?.Conversation is not { } a) return "Ukendt samtale.";
        if (!husk.IsConfigured) return "Husk er ikke sat op (Husk__ApiKey).";
        var error = await husk.SaveAsync(new HuskBall(a.HuskKey, $"Venter på svar fra {a.Who}", "venter",
            $"Du skrev {a.Date.ToLocalTime().ToString("d.M", Da)}: {Short(a.Subject)}. Intet svar endnu - skriv igen eller ring.", a.Who), ct);
        if (error is not null) return error;
        store.SaveWatched(new Watched(a.ThreadId, a.Who, a.Subject, a.Date, Hidden: false));
        return null;
    }

    public string? Hide(ulong threadId)
    {
        if (Awaiting().FirstOrDefault(a => a.Conversation.ThreadId == threadId)?.Conversation is not { } a) return "Ukendt samtale.";
        store.SaveWatched(new Watched(a.ThreadId, a.Who, a.Subject, a.Date, Hidden: true));
        return null;
    }

    private static string Name(MailHeader m) => string.IsNullOrWhiteSpace(m.FromName) ? m.FromAddress : m.FromName;

    private static string Short(string s) => string.IsNullOrWhiteSpace(s) ? "(intet emne)" : s.Length > 70 ? s[..70] + "…" : s;
}
