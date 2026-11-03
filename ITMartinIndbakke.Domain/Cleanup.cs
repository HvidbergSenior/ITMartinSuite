namespace ITMartinIndbakke.Domain;

/// <summary>
/// v2 (user 2026-11-04: "delete mails ... I know people are afraid to delete, so how to make certain that it is alright?").
/// The safety rules, in one place:
/// - only junk can be deleted (Reklame, Nyhedsbreve, Notifikationer) - never people, Digital Post, Haster, receipts or starred mail;
/// - "delete" = Gmail's Papirkurv, which Gmail keeps for 30 days, and Indbakke has its own Fortryd;
/// - the user sees how many, how old and a few subjects BEFORE anything happens.
/// </summary>
public static class CleanupRules
{
    public const int MaxPerAction = 3000;

    public static readonly MailCategory[] DeletableCategories = [MailCategory.Reklame, MailCategory.Nyhedsbreve, MailCategory.Notifikationer];

    public static bool CanDelete(MailHeader m, MailCategory c) => DeletableCategories.Contains(c) && !m.Flagged;

    /// <summary>Why a mail from this sender is kept - shown in the preview, in words.</summary>
    public static string WhyKept(MailHeader m, MailCategory c) =>
        m.Flagged ? "du har sat stjerne på den" : c switch
        {
            MailCategory.DigitalPost => "Digital Post",
            MailCategory.Haster => "den kan have en frist",
            MailCategory.SkalSvares => "den er fra et menneske",
            MailCategory.Kvitteringer => "kvitteringer gemmes (bogføring: 5 år)",
            _ => "den er ikke reklame",
        };

    /// <summary>What a press on "Ryd op" will do for one sender.</summary>
    public static CleanupPreview Preview(string address, IReadOnlyList<(MailHeader Mail, MailCategory Category)> all)
    {
        var mine = all.Where(x => string.Equals(x.Mail.FromAddress, address, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Mail.Date).ToList();
        var delete = mine.Where(x => CanDelete(x.Mail, x.Category)).Take(MaxPerAction).ToList();
        var kept = mine.Where(x => !CanDelete(x.Mail, x.Category))
            .GroupBy(x => WhyKept(x.Mail, x.Category)).Select(g => new KeptReason(g.Key, g.Count())).ToList();
        var newestWithUnsub = mine.FirstOrDefault(x => x.Mail.CanUnsubscribe).Mail;
        var target = newestWithUnsub is null ? null : UnsubscribeTarget.Parse(newestWithUnsub.ListUnsubscribe!, newestWithUnsub.OneClickUnsubscribe);
        return new CleanupPreview(
            address,
            mine.Select(x => x.Mail.FromName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? address,
            delete.Select(x => x.Mail).ToList(),
            kept,
            delete.Count == 0 ? null : delete.Min(x => x.Mail.Date),
            delete.Take(3).Select(x => x.Mail.Subject).ToList(),
            target is { IsUsable: true } ? target : null);
    }
}

public sealed record KeptReason(string Why, int Count);

public sealed record CleanupPreview(
    string Address,
    string Name,
    IReadOnlyList<MailHeader> ToDelete,
    IReadOnlyList<KeptReason> Kept,
    DateTimeOffset? Oldest,
    IReadOnlyList<string> Examples,
    UnsubscribeTarget? Unsubscribe);

/// <summary>
/// The List-Unsubscribe header: "&lt;https://...&gt;, &lt;mailto:x@y?subject=...&gt;". One-click = RFC 8058 (a POST, no page to visit).
/// </summary>
public sealed record UnsubscribeTarget(Uri? Https, string? MailTo, string? MailSubject, bool OneClick)
{
    public bool IsUsable => Https is not null || MailTo is not null;

    /// <summary>How it will happen, for the button text.</summary>
    public UnsubscribeKind Kind => OneClick && Https is not null ? UnsubscribeKind.OneClick
        : MailTo is not null ? UnsubscribeKind.Mail
        : UnsubscribeKind.Page;

    public static UnsubscribeTarget Parse(string header, bool oneClick)
    {
        Uri? https = null; string? mailTo = null, subject = null;
        foreach (var raw in header.Split(','))
        {
            var part = raw.Trim().Trim('<', '>', ' ');
            if (https is null && part.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(part, UriKind.Absolute, out var u))
                https = u;
            else if (mailTo is null && part.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = part[7..];
                var q = rest.IndexOf('?');
                mailTo = Uri.UnescapeDataString(q < 0 ? rest : rest[..q]);
                if (q >= 0)
                    subject = rest[(q + 1)..].Split('&')
                        .Where(p => p.StartsWith("subject=", StringComparison.OrdinalIgnoreCase))
                        .Select(p => Uri.UnescapeDataString(p[8..])).FirstOrDefault();
                if (!mailTo.Contains('@')) mailTo = null;
            }
        }
        return new UnsubscribeTarget(https, mailTo, subject, oneClick && https is not null);
    }
}

public enum UnsubscribeKind { OneClick, Mail, Page }

/// <summary>One press in the log, so Fortryd can put the mails back (Message-IDs only, nothing of the mail itself).</summary>
public sealed record CleanupAction(
    Guid Id,
    DateTimeOffset At,
    string Address,
    string Name,
    IReadOnlyList<string> MessageIds,
    string Unsubscribed,
    bool Restored)
{
    /// <summary>Gmail empties Papirkurv after 30 days - after that Fortryd cannot work.</summary>
    public bool CanUndo(DateTimeOffset now) => !Restored && MessageIds.Count > 0 && now - At < TimeSpan.FromDays(29);
}
