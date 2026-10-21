namespace ITMartinPoll.Domain;

// "Lav din egen afstemning" for customers (user 2026-10-21: the kundenummer opens the extras in the free apps - in Stem
// that is making your own poll). Simple polls only: a title, 2-10 choices, maybe several at once, maybe a deadline.
// No e-mails are collected on a customer's poll - Stem never keeps other people's contact data for them.
public static class CustomerPollRules
{
    public const int MaxOpen = 20, MaxOptions = 10, MaxTitle = 120, MaxBody = 1000, MaxOption = 100;

    /// <returns>Error (Danish) or the cleaned poll.</returns>
    public static (string? Error, Poll? Poll) Make(string ownerCode, string title, string body, IEnumerable<string> options,
        bool allowMultiple, DateTime? deadlineUtc, int openNow, DateTime nowUtc)
    {
        var t = title.Trim();
        if (t.Length == 0) return ("Skriv hvad afstemningen handler om.", null);
        var opts = options.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (opts.Count < 2) return ("Skriv mindst 2 svarmuligheder.", null);
        if (opts.Count > MaxOptions) return ($"Højst {MaxOptions} svarmuligheder.", null);
        if (deadlineUtc is { } d && d <= nowUtc) return ("Slutdatoen skal ligge ude i fremtiden.", null);
        if (openNow >= MaxOpen) return ($"Du har {MaxOpen} åbne afstemninger. Luk en, før du laver en ny.", null);
        return (null, new Poll
        {
            OwnerCode = ownerCode, Title = Cut(t, MaxTitle), Body = Cut(body.Trim(), MaxBody), AllowMultiple = allowMultiple,
            RequireContact = false, Deadline = deadlineUtc, IsActive = true, CreatedAt = nowUtc,
            Options = opts.Select((o, i) => new PollOption { Label = Cut(o, MaxOption), SortOrder = i }).ToList(),
        });
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}
