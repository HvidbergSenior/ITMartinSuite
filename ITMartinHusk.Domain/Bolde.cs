using System.Globalization;

namespace ITMartinHusk.Domain;

// Bolde i luften (user 2026-11-06): "I want an app that is my secretary or assistant... instead of me asking here whats not
// finished there should be an easily way for me to see all these tasks. Status. What is next step... I have adhd so I have many
// unfinished balls in the air." And: "think about this as a tool for people with adhd and autism also... used for others as well."
// So: every open thing is a ball with ONE next step. The front page shows one ball at a time; the full list is behind its own tab,
// grouped, so 30 balls never land on you at once. Same layout every time, plain words, no surprises.
// Nothing sensitive: only the short words the person (or Claude, for Martin) writes - never letters or case content.

/// <summary>InProgress = "I gang"; Waiting = "Venter på andre"; Parked = "Parkeret" (on purpose, not forgotten); Done = "Færdig".</summary>
public enum BallStatus { InProgress, Waiting, Parked, Done }

public sealed class Ball
{
    public const int MaxTitle = 80, MaxText = 200, MaxKey = 60;

    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>A stable short name ("qpark-7210755") so a helper (Claude for Martin) can update the same ball again.</summary>
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Which part of life - one of <see cref="BallRules.Areas"/> or free text.</summary>
    public string Area { get; set; } = "";
    public BallStatus Status { get; set; }
    /// <summary>The ONE next thing to do - small enough to start on.</summary>
    public string NextStep { get; set; } = "";
    /// <summary>Who the ball is with while it waits ("Q-Park", "Lukas").</summary>
    public string WaitingOn { get; set; } = "";
    /// <summary>Where it stands, in one line.</summary>
    public string Note { get; set; } = "";
    public DateTime? Deadline { get; set; }
    /// <summary>A page the ball opens with "Åbn" - only a path inside Husk ("/job"), so nothing outside gets in.</summary>
    public string Link { get; set; } = "";
    /// <summary>"Det er det næste": the person picked this one to come first.</summary>
    public bool Pinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DoneAt { get; set; }

    public bool IsOpen => Status != BallStatus.Done;

    public static Ball Create(string? title, string? key, DateTime now)
    {
        var t = HuskRules.Clean(title, MaxTitle);
        if (t.Length == 0) throw new ArgumentException("Skriv, hvad bolden hedder.");
        var k = BallRules.Key(key);
        return new Ball { Title = t, Key = k.Length > 0 ? k : BallRules.Key(t), CreatedAt = now, UpdatedAt = now };
    }

    /// <summary>Changes only what is given (null = keep). Empty text clears it.</summary>
    public void Change(BallChange c, DateTime now)
    {
        if (c.Title is not null)
        {
            var t = HuskRules.Clean(c.Title, MaxTitle);
            if (t.Length == 0) throw new ArgumentException("Skriv, hvad bolden hedder.");
            Title = t;
        }
        if (c.Area is not null) Area = HuskRules.Clean(c.Area, MaxTitle);
        if (c.NextStep is not null) NextStep = HuskRules.Clean(c.NextStep, MaxText);
        if (c.WaitingOn is not null) WaitingOn = HuskRules.Clean(c.WaitingOn, MaxTitle);
        if (c.Note is not null) Note = HuskRules.Clean(c.Note, MaxText);
        if (c.Link is not null) Link = c.Link.StartsWith('/') && !c.Link.StartsWith("//") && c.Link.Length <= 60 ? c.Link : "";
        if (c.ClearDeadline) Deadline = null;
        else if (c.Deadline is { } d) Deadline = d.Date;
        if (c.Status is { } s) SetStatus(s, now);
        UpdatedAt = now;
    }

    public void SetStatus(BallStatus s, DateTime now)
    {
        Status = s;
        DoneAt = s == BallStatus.Done ? now : null;
        if (s != BallStatus.InProgress) Pinned = false;
        UpdatedAt = now;
    }
}

/// <summary>A change to a ball; null fields stay as they are.</summary>
public sealed record BallChange(
    string? Title = null, string? Area = null, BallStatus? Status = null, string? NextStep = null, string? WaitingOn = null,
    string? Note = null, DateTime? Deadline = null, bool ClearDeadline = false, string? Link = null);

/// <summary>The groups on the "Bolde" page, in this order.</summary>
public sealed record BallGroup(BallStatus Status, string Name, string Help, List<Ball> Balls);

public static class BallRules
{
    private static readonly CultureInfo Da = new("da-DK");

    /// <summary>Parts of life to sort by. Data, not code: free text works too.</summary>
    public static readonly IReadOnlyList<string> Areas = ["Sager", "Arbejde", "Hjælpere", "Kunder", "Hjemme", "Privat"];

    /// <summary>A ball whose deadline is this close comes before the others.</summary>
    public const int SoonDays = 7;

    /// <summary>Done balls are kept this long (shown folded as "Færdig"), then forgotten.</summary>
    public const int KeepDoneDays = 60;

    public static string Name(BallStatus s) => s switch
    {
        BallStatus.InProgress => "I gang",
        BallStatus.Waiting => "Venter på andre",
        BallStatus.Parked => "Parkeret",
        _ => "Færdig",
    };

    /// <summary>Danish or English words for a status (the API takes both). Null when unknown.</summary>
    public static BallStatus? ParseStatus(string? s) => (s ?? "").Trim().ToLowerInvariant() switch
    {
        "i gang" or "igang" or "inprogress" or "in progress" or "doing" => BallStatus.InProgress,
        "venter" or "venter på andre" or "waiting" => BallStatus.Waiting,
        "parkeret" or "parked" => BallStatus.Parked,
        "færdig" or "faerdig" or "done" => BallStatus.Done,
        _ => null,
    };

    /// <summary>"Q-Park 975 kr" -> "q-park-975-kr".</summary>
    public static string Key(string? s)
    {
        var chars = (s ?? "").Trim().ToLowerInvariant()
            .Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa")
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        var k = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return k.Length > Ball.MaxKey ? k[..Ball.MaxKey].TrimEnd('-') : k;
    }

    /// <summary>The ONE ball to work on now: a deadline that has passed or is today first, then the one the person picked,
    /// then a deadline within a week, then the ball in progress that has waited longest for attention. Waiting and parked
    /// balls only come forward when their deadline is today or passed - then something must happen anyway.</summary>
    public static List<Ball> Order(IEnumerable<Ball> balls, DateTime now)
    {
        var today = now.Date;
        return balls.Where(b => b.Status == BallStatus.InProgress || (b.IsOpen && b.Deadline is { } d && d <= today))
            .OrderBy(b => b.Deadline is { } d && d <= today ? 0 : b.Pinned ? 1 : b.Deadline is { } s && s <= today.AddDays(SoonDays) ? 2 : 3)
            .ThenBy(b => b.Deadline ?? DateTime.MaxValue)
            .ThenBy(b => b.UpdatedAt)
            .ToList();
    }

    public static List<BallGroup> Groups(IEnumerable<Ball> balls, DateTime now)
    {
        var all = balls.ToList();
        var inProgress = Order(all.Where(b => b.Status == BallStatus.InProgress), now);
        return
        [
            new(BallStatus.InProgress, Name(BallStatus.InProgress), "Det, du selv skal gøre noget ved.", inProgress),
            new(BallStatus.Waiting, Name(BallStatus.Waiting), "Bolden er hos en anden. Du behøver ikke gøre noget, før de svarer.",
                all.Where(b => b.Status == BallStatus.Waiting).OrderBy(b => b.Deadline ?? DateTime.MaxValue).ThenBy(b => b.Title).ToList()),
            new(BallStatus.Parked, Name(BallStatus.Parked), "Lagt til side med vilje – ikke glemt.",
                all.Where(b => b.Status == BallStatus.Parked).OrderBy(b => b.Title).ToList()),
            new(BallStatus.Done, Name(BallStatus.Done), "Det, du er færdig med. Godt gået.",
                all.Where(b => b.Status == BallStatus.Done).OrderByDescending(b => b.DoneAt).ToList()),
        ];
    }

    /// <summary>"frist i dag", "frist i morgen", "frist om 5 dage (fre 13. nov)", "frist 1. december", "fristen var i går".</summary>
    public static string DeadlineText(DateTime deadline, DateTime now)
    {
        var days = (deadline.Date - now.Date).Days;
        return days switch
        {
            0 => "frist i dag",
            1 => "frist i morgen",
            -1 => "fristen var i går",
            < -1 => $"fristen var for {-days} dage siden",
            < 7 => $"frist på {deadline.ToString("dddd", Da)} ({deadline.ToString("d. MMM", Da).TrimEnd('.')})",
            _ => $"frist {deadline.ToString("d. MMMM", Da)}",
        };
    }

    /// <summary>User 2026-11-06: "I need reminders on the things from it... like day before and on the day".
    /// A ball with a deadline gets a push the evening before (from 19) and the morning of the day (from 7.30).
    /// The window lasts the rest of that day, so a phone that was off or a ball given its deadline at noon still gets it.
    /// The key holds the date: move the deadline and the reminders come again for the new day.</summary>
    public static List<(string Key, PushMessage Message)> Reminders(IEnumerable<Ball> balls, DateTime now, ICollection<string> sent)
    {
        var due = new List<(string, PushMessage)>();
        foreach (var b in balls.Where(x => x.IsOpen && x.Deadline is not null))
        {
            var day = b.Deadline!.Value.Date;
            var step = b.NextStep.Length > 0 ? b.NextStep : "Se bolden i Husk.";
            var date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Try("aften", day.AddDays(-1).AddHours(19), day, new($"I morgen: {b.Title}", step, "/bolde"));
            Try("dagen", day.AddHours(7.5), day.AddDays(1), new($"I dag: {b.Title}", step, "/bolde"));

            void Try(string kind, DateTime from, DateTime until, PushMessage msg)
            {
                var key = $"ball|{b.Id}|{kind}|{date}";
                if (now >= from && now < until && !sent.Contains(key)) due.Add((key, msg));
            }
        }
        return due;
    }

    public static bool Overdue(Ball b, DateTime now) => b.IsOpen && b.Deadline is { } d && d.Date <= now.Date;
}
