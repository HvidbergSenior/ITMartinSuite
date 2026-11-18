using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartinIndbakke.Domain;

// The automatic secretary (user 2026-11-18): "a lot of dates from mails are difficult to remember... they should be updated...
// I need automatic update on mails" + "add conversations where I did not get answer".
// Plain rules, no AI (feedback: no AI cost). The mail text is only read in memory - what is kept is the date, the time,
// the one keyword that made it a date (møde, frist ...), the sender and the subject. Never the text itself.

/// <summary>What the user said about a found date: OK = into Husk, Forkert = the date was wrong (moved), Ikke vigtig = hide.</summary>
public enum DateStatus { Ny, Ok, IkkeVigtig }

/// <summary>One date found in a mail.</summary>
public sealed record DateHit(DateOnly Date, TimeOnly? Time, string Word);

/// <summary>A found date as it is kept in /data/datoer.json.</summary>
public sealed record MailDate(
    string Id, string MessageId, string From, string Subject, DateOnly Date, TimeOnly? Time, string Word,
    DateStatus Status, DateTimeOffset FoundAt)
{
    public static string MakeId(string messageId, DateOnly date) => Hash(messageId + "|" + date.ToString("yyyy-MM-dd"));

    /// <summary>Short stable key - also the Husk ball key ("mail-dato-xxxxxxxx").</summary>
    internal static string Hash(string s)
    {
        var h = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(h)[..10].ToLowerInvariant();
    }

    public string HuskKey => "mail-dato-" + Id;

    /// <summary>"Møde 26.11 kl. 9.00" - the ball title in Husk (sender first, so it is clear where it came from).</summary>
    public string Title(string? who = null)
    {
        var word = Word.Length > 0 ? char.ToUpper(Word[0], CultureInfo.GetCultureInfo("da-DK")) + Word[1..] : "Dato";
        var when = Date.ToString("d.M", CultureInfo.InvariantCulture) + (Time is { } t ? " kl. " + t.ToString("H.mm", CultureInfo.InvariantCulture) : "");
        return $"{(string.IsNullOrWhiteSpace(who) ? From : who)}: {word} {when}";
    }
}

public static partial class DateFinder
{
    /// <summary>The words that make a date worth remembering. A date with none of them near it is ignored (order numbers, "sendt 3.11").</summary>
    internal static readonly string[] Words =
    [
        "rådighedssamtale", "samtale", "møde", "mødet", "frist", "fristen", "senest", "deadline", "indkaldelse", "indkaldt",
        "aftale", "aftalen", "konsultation", "undersøgelse", "blodprøve", "tid hos", "din tid", "booket", "booking",
        "besøg", "kursus", "kurset", "start", "starter", "eksamen", "betales", "forfald", "forfalder", "betalingsfrist", "høring",
        "partshøring", "svarfrist", "afgørelse", "opstart", "interview", "jobsamtale", "tandlæge", "tandplejer", "læge",
    ];

    private static readonly string[] Months =
        ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november", "december"];

    // 26.11 / 26.11.2026 / 26/11 / 26-11-2026  (not part of a longer number: no digit or dot-digit before/after)
    [GeneratedRegex(@"(?<![\d.,/])(?<d>[0-3]?\d)[./-](?<m>[01]?\d)(?:[./-](?<y>(?:20)?\d{2}))?(?![\d,]|\.\d)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericDate();

    // 4. december / 4 dec. 2026 / den 4. dec
    [GeneratedRegex(@"(?<![\d])(?<d>[0-3]?\d)\.?\s+(?<mon>jan(?:uar)?|feb(?:ruar)?|mar(?:ts)?|apr(?:il)?|maj|jun[i]?|jul[i]?|aug(?:ust)?|sep(?:t(?:ember)?)?|okt(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?(?:\s+(?<y>20\d{2}))?(?![a-zæøå])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WordDate();

    // kl. 9 / kl 9.30 / kl. 09:30 / klokken 14
    [GeneratedRegex(@"^\s*(?:,\s*)?(?:fra\s+)?(?:kl\.?|klokken)\s*(?<h>[0-2]?\d)(?:[.:](?<mi>[0-5]\d))?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Clock();

    /// <summary>
    /// The dates in a mail's text that are (a) from today up to a year ahead and (b) have a keyword within ~80 characters.
    /// The year is guessed as the next time that day comes, when the text gives none. Max <paramref name="max"/> per mail.
    /// </summary>
    public static IReadOnlyList<DateHit> Find(string? text, DateOnly today, int max = 3)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var t = text.Length > 20_000 ? text[..20_000] : text;
        var lower = t.ToLowerInvariant();
        var hits = new List<(int Pos, DateHit Hit)>();

        foreach (Match m in NumericDate().Matches(t))
            if (Make(int.Parse(m.Groups["d"].Value), int.Parse(m.Groups["m"].Value), m.Groups["y"], today) is { } d)
                Add(m, d);
        foreach (Match m in WordDate().Matches(t))
        {
            var mon = m.Groups["mon"].Value.ToLowerInvariant();
            var mi = Array.FindIndex(Months, x => x.StartsWith(mon[..3]));
            if (mi >= 0 && Make(int.Parse(m.Groups["d"].Value), mi + 1, m.Groups["y"], today) is { } d) Add(m, d);
        }

        return hits.OrderBy(h => h.Pos)
            .GroupBy(h => (h.Hit.Date, h.Hit.Time)).Select(g => g.First())
            .Take(max).Select(h => h.Hit).ToList();

        void Add(Match m, DateOnly date)
        {
            if (date < today || date > today.AddYears(1)) return;
            var word = NearestWord(lower, m.Index, m.Length);
            if (word is null) return;
            var after = t[(m.Index + m.Length)..Math.Min(t.Length, m.Index + m.Length + 30)];
            TimeOnly? time = null;
            if (Clock().Match(after) is { Success: true } c && int.Parse(c.Groups["h"].Value) is var h and < 24)
                time = new TimeOnly(h, c.Groups["mi"].Success ? int.Parse(c.Groups["mi"].Value) : 0);
            hits.Add((m.Index, new DateHit(date, time, word)));
        }
    }

    private static DateOnly? Make(int day, int month, Group year, DateOnly today)
    {
        if (month is < 1 or > 12 || day is < 1 or > 31) return null;
        int y;
        if (year.Success)
        {
            y = int.Parse(year.Value);
            if (y < 100) y += 2000;
        }
        else
        {
            y = today.Year;
            if (day > DateTime.DaysInMonth(y, month)) return null;
            if (new DateOnly(y, month, day) < today) y++;
        }
        return day <= DateTime.DaysInMonth(y, month) ? new DateOnly(y, month, day) : null;
    }

    /// <summary>The keyword nearest the date, looking ~80 characters before and ~40 after, inside the same sentence-ish window.</summary>
    internal static string? NearestWord(string lower, int index, int length)
    {
        var from = Math.Max(0, index - 80);
        var to = Math.Min(lower.Length, index + length + 40);
        var window = lower[from..to];
        var at = index - from;
        string? best = null;
        var bestDist = int.MaxValue;
        foreach (var w in Words)
        {
            for (var p = window.IndexOf(w, StringComparison.Ordinal); p >= 0; p = window.IndexOf(w, p + 1, StringComparison.Ordinal))
            {
                // whole word start (so "start" is not found inside "genstart"; "tid hos" is fine)
                if (p > 0 && char.IsLetter(window[p - 1])) continue;
                // Danish puts the word before the date ("samtale ... den 20.11"): a word after counts double distance.
                var dist = p < at ? at - (p + w.Length) : (p - (at + length)) * 2;
                if (dist < 0) dist = 0;
                if (dist < bestDist || (dist == bestDist && w.Length > best!.Length)) { best = w; bestDist = dist; }
            }
        }
        return best;
    }
}

/// <summary>One mail I sent - the newest per Gmail conversation (from Sendt post, headers only).</summary>
public sealed record SentMail(ulong ThreadId, string To, string ToName, string Subject, DateTimeOffset Date, int Recipients);

/// <summary>A conversation where I wrote last and nobody answered yet.</summary>
public sealed record Awaiting(ulong ThreadId, string To, string ToName, string Subject, DateTimeOffset Date)
{
    public int Days(DateTimeOffset now) => Math.Max(0, (int)(now - Date).TotalDays);
    public string HuskKey => "mail-venter-" + ThreadId;
    public string Who => string.IsNullOrWhiteSpace(ToName) ? To : ToName;
}

public static class AwaitingRules
{
    /// <summary>Mails the apps send by themselves (customer numbers, receipts, invitations) - nobody is expected to answer those.</summary>
    internal static readonly string[] AutomaticSubjects =
    [
        "dit kundenummer", "dit nummer hos", "tak for din besked", "martin har svaret dig", "martin har skrevet til dig",
        "vi har fået din tilmelding", "dit snak-rum", "påmindelse:", "invitation:", "unsubscribe", "afmeld",
    ];

    internal static readonly string[] AutoReplies = ["autosvar", "automatisk svar", "automatic reply", "auto-reply", "out of office", "fravær"];

    /// <summary>
    /// "Venter på svar": I wrote last in the conversation (the newest mail from someone else, not counting auto-replies,
    /// is older than mine), to a real address, between 1 and <paramref name="days"/> days ago, to at most 3 people.
    /// </summary>
    public static IReadOnlyList<Awaiting> Find(IEnumerable<SentMail> sent, IEnumerable<MailHeader> inbox, ISet<string> mine,
        DateTimeOffset now, int days = 60)
    {
        var lastFromOthers = LastFromOthers(inbox, mine);
        return sent
            .Where(s => s.ThreadId != 0 && s.Recipients is > 0 and <= 3)
            .Where(s => s.Date <= now.AddDays(-1) && s.Date >= now.AddDays(-days))
            .Where(s => !mine.Contains(s.To.ToLowerInvariant()) && !IsMachine(s.To))
            .Where(s => !AutomaticSubjects.Any(a => s.Subject.Contains(a, StringComparison.OrdinalIgnoreCase)))
            .Where(s => !(lastFromOthers.TryGetValue(s.ThreadId, out var other) && other >= s.Date))
            .OrderByDescending(s => s.Date)
            .Select(s => new Awaiting(s.ThreadId, s.To, s.ToName, s.Subject, s.Date))
            .ToList();
    }

    /// <summary>The newest real answer in a conversation after <paramref name="since"/> - null when none came yet.</summary>
    public static MailHeader? AnswerSince(ulong threadId, DateTimeOffset since, IEnumerable<MailHeader> inbox, ISet<string> mine) =>
        inbox.Where(m => m.ThreadId == threadId && m.Date > since && !mine.Contains(m.FromAddress.ToLowerInvariant()) && !IsAutoReply(m.Subject))
             .MaxBy(m => m.Date);

    internal static Dictionary<ulong, DateTimeOffset> LastFromOthers(IEnumerable<MailHeader> inbox, ISet<string> mine)
    {
        var d = new Dictionary<ulong, DateTimeOffset>();
        foreach (var m in inbox)
        {
            if (m.ThreadId == 0 || mine.Contains(m.FromAddress.ToLowerInvariant()) || IsAutoReply(m.Subject)) continue;
            if (!d.TryGetValue(m.ThreadId, out var was) || m.Date > was) d[m.ThreadId] = m.Date;
        }
        return d;
    }

    internal static bool IsAutoReply(string subject) => AutoReplies.Any(a => subject.Contains(a, StringComparison.OrdinalIgnoreCase));

    internal static bool IsMachine(string address)
    {
        var a = address.ToLowerInvariant();
        return a.Contains("noreply") || a.Contains("no-reply") || a.Contains("no_reply") || a.Contains("donotreply")
            || a.Contains("unsubscribe") || a.StartsWith("bounce") || a.Contains("mailer-daemon");
    }
}
