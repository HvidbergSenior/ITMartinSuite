namespace ITMartinIndbakke.Domain;

/// <summary>
/// "What is actually urgent now, and how do I remember" (user 2026-11-04, from the AKA / Jobnet / kommune cases: the mail only
/// says "du har fået post", the real deadline is inside the letter, and nothing reminds you).
/// So every Digital Post / Haster mail stays in "Det haster nu" until the user marks it Klaret. Only the date, the status and
/// a few of the user's own words are kept - never the letter's content.
/// </summary>
public enum FollowUpStatus { Ny, Laest, Klaret }

public sealed record FollowUp(string MessageId, FollowUpStatus Status, DateOnly? Deadline, string Note)
{
    public const int MaxNote = 80;

    public static FollowUp New(string messageId) => new(messageId, FollowUpStatus.Ny, null, "");

    public FollowUp Read(DateOnly? deadline, string? note) =>
        this with { Status = FollowUpStatus.Laest, Deadline = deadline, Note = Clean(note) };

    public FollowUp Done() => this with { Status = FollowUpStatus.Klaret };

    public FollowUp Reopen() => this with { Status = FollowUpStatus.Laest };

    private static string Clean(string? note)
    {
        var n = (note ?? "").Trim().ReplaceLineEndings(" ");
        return n.Length > MaxNote ? n[..MaxNote] : n;
    }
}

/// <summary>One line in "Det haster nu".</summary>
public sealed record UrgentItem(MailHeader Mail, MailCategory Category, FollowUp FollowUp)
{
    /// <summary>Days left to the deadline (negative = overdue), or null without a deadline.</summary>
    public int? DaysLeft(DateOnly today) => FollowUp.Deadline is { } d ? d.DayNumber - today.DayNumber : null;
}

public static class UrgentRules
{
    /// <summary>
    /// The order: overdue first, then the nearest deadline, then letters nobody has opened yet, then read ones without a date.
    /// Klaret is not shown. A mail the user has touched (Læst, with or without a date) stays until Klaret, however old.
    /// An untouched one shows for <paramref name="newDays"/> days - so the first scan of a year is not 80 old e-Boks notices.
    /// </summary>
    public static IReadOnlyList<UrgentItem> Now(IEnumerable<(MailHeader Mail, MailCategory Category)> mails,
        IReadOnlyDictionary<string, FollowUp> followUps, DateOnly today, int newDays = 45, DateTimeOffset? baseline = null)
    {
        return mails
            .Where(m => m.Category is MailCategory.DigitalPost or MailCategory.Haster)
            .Select(m => new UrgentItem(m.Mail, m.Category,
                followUps.TryGetValue(m.Mail.MessageId, out var f) ? f : FollowUp.New(m.Mail.MessageId)))
            .Where(i => i.FollowUp.Status != FollowUpStatus.Klaret)
            // The base line (user 2026-11-04: "a base button - all mails before this are handled"): older mails are gone,
            // except the ones the user gave a deadline - those were chosen on purpose.
            .Where(i => baseline is null || i.Mail.Date >= baseline || i.FollowUp is { Status: FollowUpStatus.Laest, Deadline: not null })
            .Where(i => i.FollowUp.Status != FollowUpStatus.Ny || DateOnly.FromDateTime(i.Mail.Date.UtcDateTime) >= today.AddDays(-newDays))
            .OrderBy(i => Rank(i, today))
            .ThenBy(i => i.DaysLeft(today) ?? int.MaxValue)
            .ThenByDescending(i => i.Mail.Date)
            .ToList();
    }

    private static int Rank(UrgentItem i, DateOnly today) => i switch
    {
        _ when i.DaysLeft(today) is < 0 => 0,
        _ when i.DaysLeft(today) is not null => 1,
        _ when i.FollowUp.Status == FollowUpStatus.Ny => 2,
        _ => 3,
    };
}
