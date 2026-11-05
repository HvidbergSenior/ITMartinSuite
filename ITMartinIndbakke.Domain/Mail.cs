namespace ITMartinIndbakke.Domain;

/// <summary>Gmail's own tab for a mail (found with a Gmail search, not by reading the mail).</summary>
public enum GmailTab { None, Promotions, Social, Updates, Forums }

/// <summary>The categories the user asked for (2026-11-04): Digital Post first ("a lot of Digital post are important"), then urgent, needs a reply, the rest sorted.</summary>
public enum MailCategory { DigitalPost, Haster, SkalSvares, Kvitteringer, Nyhedsbreve, Reklame, Notifikationer, Andet }

/// <summary>
/// One mail as Indbakke sees it: ONLY the headers (sender, subject, date, flags, list headers). Never the text of the mail -
/// so nothing private is read or kept (see feedback: no sensitive data).
/// </summary>
public sealed record MailHeader(
    uint Uid,
    string MessageId,
    string FromAddress,
    string FromName,
    string Subject,
    DateTimeOffset Date,
    bool Seen,
    bool Answered,
    bool ToMeDirectly,
    string? ListUnsubscribe,
    bool OneClickUnsubscribe,
    bool IsMailingList,
    bool IsAutomatic,
    GmailTab Tab,
    bool Flagged = false,   // Gmail star = IMAP \Flagged; starred mail is never deleted
    bool RepliedLater = false)   // I wrote later in the same Gmail conversation (from Sendt post) - also replies from the phone
{
    public string Domain => FromAddress.Contains('@') ? FromAddress[(FromAddress.LastIndexOf('@') + 1)..].ToLowerInvariant() : "";

    /// <summary>The sender offers an unsubscribe (a link or a mail address in the List-Unsubscribe header).</summary>
    public bool CanUnsubscribe => !string.IsNullOrWhiteSpace(ListUnsubscribe);
}

/// <summary>All mails from one sender - the row the user cleans up in one go.</summary>
public sealed record SenderGroup(
    string Address,
    string Name,
    int Count,
    int Unread,
    DateTimeOffset Newest,
    MailCategory Category,
    bool CanUnsubscribe,
    bool OneClick);

/// <summary>The result of one scan.</summary>
public sealed record MailOverview(
    DateTimeOffset ScannedAt,
    int Months,
    int Total,
    int Unread,
    IReadOnlyDictionary<MailCategory, int> PerCategory,
    /// <summary>All Digital Post + Haster mails (newest first) - "Det haster nu" is made from these plus the follow-ups.</summary>
    IReadOnlyList<(MailHeader Mail, MailCategory Category)> Important,
    IReadOnlyList<(MailHeader Mail, MailCategory Category)> NeedsReply,
    IReadOnlyList<SenderGroup> Senders,
    IReadOnlyList<(MailHeader Mail, MailCategory Category)> All);
