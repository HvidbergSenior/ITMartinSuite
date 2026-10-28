namespace ITMartinPoll.Domain;

// "Lav din egen afstemning" for customers (user 2026-10-21: the kundenummer opens the extras in the free apps - in Stem
// that is making your own poll). FREE = one open poll OF EACH KIND (rule B, user 2026-10-21; 2026-10-28: a link with
// several questions takes every kind it asks), open for at most 14 days ("maybe only 14 days not month"); more is the
// paid version. A title, 2-10 answers (a picture on each if you like), maybe several at once, maybe a deadline.
// No e-mails are collected on a customer's poll - Stem never keeps other people's contact data for them.
public static class CustomerPollRules
{
    public const int MaxDays = 14, MaxOptions = 10, MaxTitle = 120, MaxBody = 1000, MaxOption = 100, MaxQuestions = 10;

    /// <param name="openKinds">The kinds the customer already has open (QuestionKinds) - each kind once in the free version.</param>
    /// <param name="images">A picture per answer, same order as options (null or shorter = no picture).</param>
    /// <returns>Error (Danish) or the cleaned poll.</returns>
    public static (string? Error, Poll? Poll) Make(string ownerCode, string title, string body, IEnumerable<string> options,
        bool allowMultiple, DateTime? deadlineUtc, IReadOnlyCollection<string> openKinds, DateTime nowUtc, string kind = QuestionKinds.Choose,
        IReadOnlyList<string?>? images = null)
    {
        var t = title.Trim();
        if (t.Length == 0) return ("Skriv hvad afstemningen handler om.", null);
        if (!QuestionKinds.Valid(kind)) kind = QuestionKinds.Choose;
        var opts = Answers(options, kind == QuestionKinds.Days ? null : images);
        if (opts.Count < 2 && kind == QuestionKinds.Days) return ("Vælg en periode og nogle ugedage, så der bliver mindst 2 dage.", null);
        if (opts.Count < 2) return ("Skriv mindst 2 svarmuligheder.", null);
        var max = kind == QuestionKinds.Days ? QuestionKinds.MaxDays : MaxOptions;
        if (opts.Count > max) return ($"Højst {max} svarmuligheder.", null);
        if (deadlineUtc is { } d && d <= nowUtc) return ("Slutdatoen skal ligge ude i fremtiden.", null);
        // No end date, or one further away than a month: it closes by itself after MaxDays.
        var last = nowUtc.AddDays(MaxDays);
        if (deadlineUtc is null || deadlineUtc > last) deadlineUtc = last;
        if (openKinds.Contains(kind)) return (FreeLimit(kind), null);
        return (null, new Poll
        {
            OwnerCode = ownerCode, Title = Cut(t, MaxTitle), Body = Cut(body.Trim(), MaxBody), Kind = kind,
            AllowMultiple = kind == QuestionKinds.Days || allowMultiple,   // you can be free on several days
            RequireContact = false, Deadline = deadlineUtc, IsActive = true, CreatedAt = nowUtc,
            Options = opts.Select((o, i) => new PollOption { Label = Cut(o.Label, MaxOption), ImageName = o.Image, SortOrder = i }).ToList(),
        });
    }

    // The answers as typed, each with its picture: an answer with only a picture is called "Billede n"; blanks and
    // repeats go.
    private static List<(string Label, string? Image)> Answers(IEnumerable<string> options, IReadOnlyList<string?>? images)
    {
        var list = new List<(string Label, string? Image)>();
        var i = 0;
        foreach (var o in options)
        {
            var image = images is not null && i < images.Count && IsStoredImage(images[i]) ? images[i] : null;
            i++;
            var label = o.Trim();
            if (label.Length == 0 && image is not null) label = $"Billede {i}";
            if (label.Length == 0 || list.Any(x => string.Equals(x.Label, label, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add((label, image));
        }
        return list;
    }

    /// <summary>Only a name the picture store made (prefix + 32 hex + .jpg) - never a path someone typed.</summary>
    public static bool IsStoredImage(string? name) =>
        name is not null && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z]{0,4}-?[0-9a-f]{32}\.jpg$");

    /// <summary>Said when the customer already has an open one of that kind.</summary>
    public static string FreeLimit(string kind) =>
        $"I den gratis udgave har du én åben afstemning af hver slags, og du har allerede en åben med „{QuestionKinds.Name(kind)}“. " +
        "Luk den (under Mine afstemninger), før du laver en ny – eller spørg Martin om den udvidede udgave.";

    /// <summary>Several questions in one link (user 2026-10-21: "I can only choose Hvad vil du spørge om?" -> "only one
    /// question per poll"): a package that anyone with the link may answer, with or without a name. Each question has its
    /// own answers and its own "vælg flere"; the link takes every kind it asks (rule B).</summary>
    /// <returns>Error (Danish) or the package with its questions.</returns>
    public static (string? Error, Package? Package) MakeSurvey(string ownerCode, string title, string intro, IReadOnlyList<QuestionDraft> questions,
        DateTime? deadlineUtc, IReadOnlyCollection<string> openKinds, DateTime nowUtc)
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
            var (error, poll) = Make(ownerCode, q.Text, "", q.Options, q.AllowMultiple, deadlineUtc, [], nowUtc, q.Kind, q.Images);
            if (error is not null) return ($"Spørgsmål {i + 1}: {error}", null);
            polls.Add(poll!);
        }
        if (polls.Select(p => p.Kind).FirstOrDefault(openKinds.Contains) is { } busy) return (FreeLimit(busy), null);
        return (null, new Package
        {
            OwnerCode = ownerCode, Title = Cut(t, MaxTitle), Intro = Cut(intro.Trim(), MaxBody), Audience = "", IsActive = true, OpenByLink = true,
            CreatedAt = nowUtc, Instructions = "Skriv dit navn, hvis du vil.\nSvar på spørgsmålene – dine svar bliver gemt med det samme.\nKom tilbage til linket for at rette dine svar.",
            Polls = polls,
        });
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}

/// <summary>One question in a customer's poll: the question, its answers, and whether one may pick several.</summary>
/// Images = a picture per answer (same order as Options), for "Vælg svar" and "Giv karakter".
public sealed record QuestionDraft(string Text, IReadOnlyList<string> Options, bool AllowMultiple, string Kind = QuestionKinds.Choose,
    IReadOnlyList<string?>? Images = null);

/// <summary>The three kinds of question a customer can ask (user 2026-10-21: "Which brand do you think is the prettiest?
/// And give grades... And which days?" -> "maybe make 3 choices").</summary>
public static class QuestionKinds
{
    public const string Choose = "", Grade = "karakter", Days = "dage";
    public const int MaxScore = 5, MaxDays = 93;   // every day for 3 months
    public static readonly string[] All = [Choose, Grade, Days];
    public static bool Valid(string kind) => kind is Choose or Grade or Days;
    public static string Name(string kind) => kind switch { Grade => "Giv karakter", Days => "Vælg dage", _ => "Vælg svar" };
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

    /// <summary>The dates behind the labels (they carry no year): the first label is in the year the question was made,
    /// or the next year when its month is well before that; later labels move to the next year when the month goes back.
    /// Lets the answer page hide days that have passed and put the rest under month headings.</summary>
    /// <returns>One date per label, in the same order; null for a label that is not a day.</returns>
    public static List<DateOnly?> Dates(IEnumerable<string> labels, DateOnly madeOn)
    {
        var result = new List<DateOnly?>();
        int? year = null, lastMonth = null;
        foreach (var label in labels)
        {
            var parts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var month = parts.Length == 3 ? Array.IndexOf(Months, parts[2]) + 1 : 0;
            if (month == 0 || !int.TryParse(parts[1].TrimEnd('.'), out var day)) { result.Add(null); continue; }
            year ??= month < madeOn.Month - 1 ? madeOn.Year + 1 : madeOn.Year;
            if (lastMonth is { } m && month < m) year++;
            lastMonth = month;
            result.Add(day <= DateTime.DaysInMonth(year.Value, month) ? new DateOnly(year.Value, month, day) : null);
        }
        return result;
    }

    private static readonly string[] MonthNames = ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august",
        "september", "oktober", "november", "december"];
    public static string MonthHeading(DateOnly d) => $"{MonthNames[d.Month - 1]} {d.Year}";
}

