namespace ITMartinPoll.Domain;

// "Lav din egen afstemning" for customers (user 2026-10-21: the kundenummer opens the extras in the free apps - in Stem
// that is making your own poll). FREE = one open poll at a time (user 2026-10-21: "i think the free version is. One poll
// at a time"), open for at most 14 days ("also a limit on existing... like a month" -> "maybe only 14 days not month"); more is the paid version. Simple polls only: a title, 2-10 choices, maybe several at once, maybe a deadline.
// No e-mails are collected on a customer's poll - Stem never keeps other people's contact data for them.
public static class CustomerPollRules
{
    public const int MaxOpen = 1, MaxDays = 14, MaxOptions = 10, MaxTitle = 120, MaxBody = 1000, MaxOption = 100, MaxQuestions = 10;

    /// <returns>Error (Danish) or the cleaned poll.</returns>
    public static (string? Error, Poll? Poll) Make(string ownerCode, string title, string body, IEnumerable<string> options,
        bool allowMultiple, DateTime? deadlineUtc, int openNow, DateTime nowUtc, string kind = QuestionKinds.Choose)
    {
        var t = title.Trim();
        if (t.Length == 0) return ("Skriv hvad afstemningen handler om.", null);
        var opts = options.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (opts.Count < 2) return ("Skriv mindst 2 svarmuligheder.", null);
        if (!QuestionKinds.Valid(kind)) kind = QuestionKinds.Choose;
        var max = kind == QuestionKinds.Days ? QuestionKinds.MaxDays : MaxOptions;
        if (opts.Count < 2 && kind == QuestionKinds.Days) return ("Vælg en periode og nogle ugedage, så der bliver mindst 2 dage.", null);
        if (opts.Count > max) return ($"Højst {max} svarmuligheder.", null);
        if (deadlineUtc is { } d && d <= nowUtc) return ("Slutdatoen skal ligge ude i fremtiden.", null);
        // No end date, or one further away than a month: it closes by itself after MaxDays.
        var last = nowUtc.AddDays(MaxDays);
        if (deadlineUtc is null || deadlineUtc > last) deadlineUtc = last;
        if (openNow >= MaxOpen) return (FreeLimit, null);
        return (null, new Poll
        {
            OwnerCode = ownerCode, Title = Cut(t, MaxTitle), Body = Cut(body.Trim(), MaxBody), Kind = kind,
            AllowMultiple = kind == QuestionKinds.Days || allowMultiple,   // you can be free on several days
            RequireContact = false, Deadline = deadlineUtc, IsActive = true, CreatedAt = nowUtc,
            Options = opts.Select((o, i) => new PollOption { Label = Cut(o, MaxOption), SortOrder = i }).ToList(),
        });
    }

    public const string FreeLimit = "I den gratis udgave har du én åben afstemning ad gangen. Luk den, du har (under Mine afstemninger), " +
        "før du laver en ny – eller spørg Martin om den udvidede udgave.";

    /// <summary>Several questions in one link (user 2026-10-21: "I can only choose Hvad vil du spørge om?" -> "only one
    /// question per poll"): a package that anyone with the link may answer, with their name. Each question has its own
    /// answers and its own "vælg flere".</summary>
    /// <returns>Error (Danish) or the package with its questions.</returns>
    public static (string? Error, Package? Package) MakeSurvey(string ownerCode, string title, string intro, IReadOnlyList<QuestionDraft> questions,
        DateTime? deadlineUtc, int openNow, DateTime nowUtc)
    {
        var t = title.Trim();
        if (t.Length == 0) return ("Skriv hvad det handler om.", null);
        var qs = questions.Where(q => q.Text.Trim().Length > 0 || q.Options.Any(o => o.Trim().Length > 0)).ToList();
        if (qs.Count == 0) return ("Skriv mindst ét spørgsmål.", null);
        if (qs.Count > MaxQuestions) return ($"Højst {MaxQuestions} spørgsmål.", null);
        var polls = new List<Poll>();
        for (var i = 0; i < qs.Count; i++)
        {
            var q = qs[i];
            if (q.Text.Trim().Length == 0) return ($"Skriv spørgsmål {i + 1}.", null);
            var (error, poll) = Make(ownerCode, q.Text, "", q.Options, q.AllowMultiple, deadlineUtc, 0, nowUtc, q.Kind);
            if (error is not null) return ($"Spørgsmål {i + 1}: {error}", null);
            polls.Add(poll!);
        }
        if (openNow >= MaxOpen) return (FreeLimit, null);
        return (null, new Package
        {
            OwnerCode = ownerCode, Title = Cut(t, MaxTitle), Intro = Cut(intro.Trim(), MaxBody), Audience = "", IsActive = true, OpenByLink = true,
            CreatedAt = nowUtc, Instructions = "Skriv dit navn.\nSvar på spørgsmålene – dine svar bliver gemt med det samme.\nKom tilbage til linket for at rette dine svar.",
            Polls = polls,
        });
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}

/// <summary>One question in a customer's poll: the question, its answers, and whether one may pick several.</summary>
public sealed record QuestionDraft(string Text, IReadOnlyList<string> Options, bool AllowMultiple, string Kind = QuestionKinds.Choose);

/// <summary>The three kinds of question a customer can ask (user 2026-10-21: "Which brand do you think is the prettiest?
/// And give grades... And which days?" -> "maybe make 3 choices").</summary>
public static class QuestionKinds
{
    public const string Choose = "", Grade = "karakter", Days = "dage";
    public const int MaxScore = 5, MaxDays = 93;   // every day for 3 months
    public static bool Valid(string kind) => kind is Choose or Grade or Days;
}

/// <summary>"Vælg dage": the days in a period on the chosen weekdays become the answers - "hver mandag", "alle hverdage",
/// "weekender".</summary>
public static class DayRules
{
    public static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    public static readonly DayOfWeek[] Weekend = [DayOfWeek.Saturday, DayOfWeek.Sunday];
    private static readonly string[] Short = ["søn", "man", "tir", "ons", "tor", "fre", "lør"];
    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    public static string Label(DateOnly d) => $"{Short[(int)d.DayOfWeek]} {d.Day}. {Months[d.Month - 1]}";

    /// <returns>The labels, at most QuestionKinds.MaxDays; empty when the period or the weekdays give nothing.</returns>
    public static List<string> Days(DateOnly from, DateOnly to, IReadOnlyCollection<DayOfWeek> on)
    {
        var list = new List<string>();
        for (var d = from; d <= to && list.Count < QuestionKinds.MaxDays; d = d.AddDays(1))
            if (on.Contains(d.DayOfWeek)) list.Add(Label(d));
        return list;
    }
}

