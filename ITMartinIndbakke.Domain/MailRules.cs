namespace ITMartinIndbakke.Domain;

/// <summary>
/// The sorting rules - plain rules on the headers, no AI (user rule 2026-10-31 "no cost on ai").
/// Order matters: Haster first, then receipts, then real people waiting for an answer, then the bulk.
/// </summary>
public static class MailRules
{
    /// <summary>Digital Post: the notices that a letter is waiting (the letter itself is behind MitID - we only see the notice).</summary>
    internal static readonly string[] DigitalPostDomains = ["e-boks.dk", "e-boks.com", "mit.dk", "digitalpost.dk", "post.borger.dk", "borger.dk"];

    internal static readonly string[] DigitalPostWords = ["digital post", "ny post", "fået post", "nyt brev", "e-boks", "mit.dk"];

    /// <summary>Authorities, banks, a-kasser, health: mail from them can carry a deadline.</summary>
    internal static readonly string[] AuthorityDomains =
    [
        "skat.dk", "borger.dk", "e-boks.dk", "e-boks.com", "mitid.dk", "digst.dk", "sundhed.dk", "politi.dk", "domstol.dk",
        "kommune.dk", "atp.dk", "udbetalingdanmark.dk", "star.dk", "jobnet.dk", "nets.eu", "mitid.nets.eu",
        "danskebank.dk", "nordea.dk", "nordea.com", "jyskebank.dk", "sydbank.dk", "spks.dk", "lsb.dk", "al-bank.dk", "arbejdernes-landsbank.dk",
        "nykredit.dk", "realkredit.dk", "totalkredit.dk", "lunar.app", "mobilepay.dk",
        "aka.dk", "ase.dk", "3f.dk", "krifa.dk", "dana.dk", "ca.dk", "hk.dk", "fta.dk", "min-a-kasse.dk", "akademikernes.dk", "magistrenes.dk",
    ];

    internal static readonly string[] UrgentWords =
    [
        "frist", "senest", "rykker", "inkasso", "haster", "vigtigt", "forfald", "underretning", "opsigelse", "påmindelse om betaling",
        "deadline", "urgent", "betalingspåmindelse", "sidste chance for at svare", "mangler", "afgørelse", "partshøring", "indkaldelse",
    ];

    internal static readonly string[] ReceiptWords =
    [
        "kvittering", "ordrebekræftelse", "ordre", "faktura", "betaling modtaget", "din pakke", "forsendelse", "levering", "afsendt",
        "receipt", "invoice", "order", "your package", "shipped", "booking", "billet", "bekræftelse",
    ];

    internal static readonly string[] PersonalDomains =
    [
        "gmail.com", "hotmail.com", "hotmail.dk", "outlook.com", "outlook.dk", "live.dk", "live.com", "yahoo.com", "yahoo.dk",
        "icloud.com", "me.com", "mail.dk", "mail.tele.dk", "jubii.dk", "ofir.dk", "mensa.dk", "protonmail.com", "proton.me",
    ];

    internal static readonly string[] MachineSenders =
    [
        "noreply", "no-reply", "no_reply", "donotreply", "do-not-reply", "notification", "notifications", "notify", "mailer-daemon",
        "postmaster", "alerts", "alert@", "automated", "system@", "info@", "nyhedsbrev", "newsletter", "news@", "marketing",
    ];

    /// <summary>Sort one mail. <paramref name="wroteTo"/> = addresses the user has written to (from Sendt post);
    /// <paramref name="mine"/> = the user's own addresses (the From of Sendt post) - mail from yourself never waits for an answer.</summary>
    public static MailCategory Categorize(MailHeader m, ISet<string> wroteTo, ISet<string>? mine = null)
    {
        var subject = m.Subject.ToLowerInvariant();
        var from = m.FromAddress.ToLowerInvariant();
        if (mine?.Contains(from) == true) return MailCategory.Andet;
        var bulk = m.IsMailingList || m.CanUnsubscribe || m.Tab == GmailTab.Promotions;

        if (IsDigitalPost(m, subject)) return MailCategory.DigitalPost;
        if (IsAuthority(m.Domain) && !bulk) return MailCategory.Haster;
        // A deadline word counts from a person or an authority - not from a machine (Google's "Sikkerhedsunderretning" x 84).
        if (!bulk && ContainsAny(subject, UrgentWords) && !m.IsAutomatic && !ContainsAny(from, MachineSenders)) return MailCategory.Haster;

        if (ContainsAny(subject, ReceiptWords) && !(m.Tab == GmailTab.Promotions)) return MailCategory.Kvitteringer;

        if (IsPerson(m, from, wroteTo)) return MailCategory.SkalSvares;

        if (m.Tab == GmailTab.Promotions) return MailCategory.Reklame;
        if (m.Tab is GmailTab.Social or GmailTab.Updates or GmailTab.Forums && !m.IsMailingList) return MailCategory.Notifikationer;
        if (m.IsMailingList || m.CanUnsubscribe) return MailCategory.Nyhedsbreve;
        if (m.IsAutomatic || ContainsAny(from, MachineSenders)) return MailCategory.Notifikationer;
        return MailCategory.Andet;
    }

    /// <summary>A real person wrote to me: not a list, not a machine, I am in To, and I know them or they write from a private mailbox.</summary>
    internal static bool IsPerson(MailHeader m, string from, ISet<string> wroteTo)
    {
        if (m.IsMailingList || m.CanUnsubscribe || m.IsAutomatic || m.Tab == GmailTab.Promotions) return false;
        if (ContainsAny(from, MachineSenders)) return false;
        if (wroteTo.Contains(from)) return true;
        return m.ToMeDirectly && PersonalDomains.Contains(m.Domain);
    }

    /// <summary>"Skal svares" on the list = a person, not answered yet (IMAP \Answered), not older than <paramref name="days"/>.</summary>
    public static bool WaitsForReply(MailHeader m, MailCategory c, DateTimeOffset now, int days = 60) =>
        c == MailCategory.SkalSvares && !m.Answered && m.Date >= now.AddDays(-days);

    internal static bool IsDigitalPost(MailHeader m, string subject) =>
        DigitalPostDomains.Any(d => m.Domain == d || m.Domain.EndsWith("." + d))
        || (IsAuthority(m.Domain) && ContainsAny(subject, DigitalPostWords));

    internal static bool IsAuthority(string domain) =>
        domain.Length > 0 && (AuthorityDomains.Any(d => domain == d || domain.EndsWith("." + d)) || domain.EndsWith("kommune.dk"));

    private static bool ContainsAny(string text, string[] words) => words.Any(text.Contains);

    /// <summary>The whole overview from the scanned headers.</summary>
    public static MailOverview Build(IReadOnlyList<MailHeader> mails, ISet<string> wroteTo, int months, DateTimeOffset now, ISet<string>? mine = null)
    {
        var sorted = mails.Select(m => (Mail: m, Category: Categorize(m, wroteTo, mine))).ToList();

        var perCategory = Enum.GetValues<MailCategory>().ToDictionary(c => c, c => sorted.Count(s => s.Category == c));

        var important = sorted.Where(s => s.Category is MailCategory.DigitalPost or MailCategory.Haster)
            .OrderByDescending(s => s.Mail.Date).Take(300).ToList();
        var reply = sorted.Where(s => WaitsForReply(s.Mail, s.Category, now))
            .OrderByDescending(s => s.Mail.Date).Take(50).ToList();

        var senders = sorted
            .GroupBy(s => s.Mail.FromAddress.ToLowerInvariant())
            .Select(g =>
            {
                var newest = g.MaxBy(s => s.Mail.Date);
                var category = g.GroupBy(s => s.Category).OrderByDescending(c => c.Count()).First().Key;
                return new SenderGroup(
                    g.Key,
                    g.Select(s => s.Mail.FromName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                    g.Count(),
                    g.Count(s => !s.Mail.Seen),
                    newest.Mail.Date,
                    category,
                    g.Any(s => s.Mail.CanUnsubscribe),
                    g.Any(s => s.Mail.OneClickUnsubscribe));
            })
            .OrderByDescending(s => s.Count).ThenByDescending(s => s.Newest)
            .ToList();

        return new MailOverview(now, months, mails.Count, mails.Count(m => !m.Seen), perCategory, important, reply, senders);
    }
}
