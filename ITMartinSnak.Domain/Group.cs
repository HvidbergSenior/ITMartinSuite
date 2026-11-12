using System.Security.Cryptography;
using System.Text;

namespace ITMartinSnak.Domain;

// The shapes are kept exactly as they were stored in messages.json / tasks.json since 2026-10-09 - existing files read as before.
// Host = written by Martin (the room owner) - the overview shows who is waiting for an answer (2026-11-04).
public sealed record Message(long Id, string Name, string Text, DateTime At, bool Host = false);

// 2026-11-08 TODAY (user: "combine with today. So all people can see what happens today and today only"): Day = the date it is
// for ("yyyy-MM-dd", "" = no day), Daily = every day (medicine, a phone call) - its tick (DoneOn) only counts for that day.
// 2026-11-12 (family room Skejby, user: "a way for each person to add their important dates"): By = the member who added it
// ("" = the host) - that person may change and remove it too; Yearly = comes back on the same date every year (birthdays),
// its tick (DoneOn) only counts for that year's day, like Daily.
public sealed record TaskItem(long Id, string Title, string Note, string When, List<string> Takers, bool Done, DateTime CreatedAt,
    string Day = "", bool Daily = false, string DoneOn = "", string By = "", bool Yearly = false);

// The group's rules (user 2026-10-09: a small chat for a few friends instead of a Facebook group; Martin makes the tasks,
// the others chat, see the tasks and put themselves on one).
public static class GroupRules
{
    public const int MaxMessages = 5000, MaxText = 4000, MaxName = 40, MaxTitle = 120, MaxNote = 1000, MaxWhen = 60;

    public static string CleanName(string? name) => Cut((name ?? "").Trim(), MaxName);

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;

    /// <returns>The new message, or null when the name or the text is missing.</returns>
    public static Message? NewMessage(IReadOnlyList<Message> existing, string? name, string? text, DateTime nowUtc, bool host = false)
    {
        var n = CleanName(name);
        var t = Cut((text ?? "").Trim(), MaxText);
        if (n.Length == 0 || t.Length == 0) return null;
        return new Message((existing.Count == 0 ? 0 : existing[^1].Id) + 1, n, t, nowUtc, host);
    }

    /// <summary>Only the newest <see cref="MaxMessages"/> are kept.</summary>
    public static void Trim(List<Message> messages)
    {
        if (messages.Count > MaxMessages) messages.RemoveRange(0, messages.Count - MaxMessages);
    }

    /// <param name="to">2026-11-08: the task can be given to someone straight away (shared rooms) - empty = anyone can take it.</param>
    /// <param name="by">The member who adds it ("" = the host).</param>
    /// <param name="yearly">Every year on the day's date - only with a real day, never together with daily.</param>
    public static TaskItem? NewTask(IReadOnlyList<TaskItem> existing, string? title, string? note, string? when, DateTime nowUtc, string? to = null,
        string? day = null, bool daily = false, string? by = null, bool yearly = false)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        var n = CleanName(to);
        var d = daily ? "" : CleanDay(day);
        return new TaskItem((existing.Count == 0 ? 0 : existing.Max(x => x.Id)) + 1, Cut(ti, MaxTitle), Cut((note ?? "").Trim(), MaxNote),
            Cut((when ?? "").Trim(), MaxWhen), n.Length > 0 ? [n] : [], false, nowUtc, d, daily, "", CleanName(by), yearly && d.Length > 0);
    }

    /// <summary>Only a real date "yyyy-MM-dd" is kept.</summary>
    public static string CleanDay(string? day) =>
        DateOnly.TryParseExact((day ?? "").Trim(), "yyyy-MM-dd", out var d) ? d.ToString("yyyy-MM-dd") : "";

    /// <summary>Is the task for this date: every day, its own date, or - yearly - the same day and month (29 February falls on
    /// the 28th in the other years).</summary>
    public static bool IsOn(TaskItem t, DateOnly date)
    {
        if (t.Daily) return true;
        if (!t.Yearly) return t.Day == date.ToString("yyyy-MM-dd");
        if (!DateOnly.TryParseExact(t.Day, "yyyy-MM-dd", out var d)) return false;
        var day = d.Month == 2 && d.Day == 29 && !DateTime.IsLeapYear(date.Year) ? 28 : d.Day;
        return date.Month == d.Month && date.Day == day;
    }

    /// <summary>The next date it falls on, today included ("" = no day). A yearly one moves to the coming year.</summary>
    public static string NextDay(TaskItem t, DateOnly today)
    {
        if (!t.Yearly) return t.Day;
        for (var y = today.Year; y <= today.Year + 1; y++)
        {
            var d = DateOnly.Parse(t.Day);
            var date = new DateOnly(y, d.Month, d.Month == 2 && d.Day == 29 && !DateTime.IsLeapYear(y) ? 28 : d.Day);
            if (date >= today) return date.ToString("yyyy-MM-dd");
        }
        return t.Day;
    }

    /// <summary>Done: a daily or yearly task only for that day (it comes back), any other task for good.</summary>
    public static bool IsDoneOn(TaskItem t, DateOnly today) =>
        t.Daily || t.Yearly ? t.DoneOn == today.ToString("yyyy-MM-dd") : t.Done;

    /// <summary>Ticked off: a daily task only for today (it comes back tomorrow), a yearly one only for this year's day, any other for good.</summary>
    public static TaskItem SetDone(TaskItem t, bool done, DateOnly today) =>
        t.Daily || t.Yearly ? t with { Done = false, DoneOn = done ? today.ToString("yyyy-MM-dd") : "" } : t with { Done = done };

    /// <summary>What "I dag" shows: daily tasks, today's (also yearly ones), and the ones with no day that are not done.</summary>
    public static bool IsToday(TaskItem t, DateOnly today) =>
        IsOn(t, today) || (t.Day.Length == 0 && !t.Done);

    /// <param name="to">null = keep who has it; "" = nobody; a name = give it to that person.</param>
    /// <param name="yearly">null = keep; a yearly task needs a day and is never daily.</param>
    public static TaskItem? Edit(TaskItem t, string? title, string? note, string? when, string? to = null, string? day = null, bool? daily = null,
        bool? yearly = null)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        var takers = to is null ? t.Takers.ToList() : CleanName(to) is { Length: > 0 } n ? [n] : new List<string>();
        var d = daily ?? t.Daily;
        var newDay = d ? "" : day is null ? t.Day : CleanDay(day);
        var y = !d && newDay.Length > 0 && (yearly ?? t.Yearly);
        return t with
        {
            Title = Cut(ti, MaxTitle), Note = Cut((note ?? "").Trim(), MaxNote), When = Cut((when ?? "").Trim(), MaxWhen), Takers = takers,
            Daily = d, Yearly = y, Day = newDay, DoneOn = d || y ? t.DoneOn : "",
        };
    }

    /// <summary>The host changes everything; a member only what they added themselves (same name, case doesn't matter).</summary>
    public static bool MayChange(TaskItem t, string? name, bool host) =>
        host || (t.By.Length > 0 && string.Equals(t.By, CleanName(name), StringComparison.OrdinalIgnoreCase));

    /// <summary>Puts the name on the task, or takes it off again if it is already there (case doesn't matter).</summary>
    public static TaskItem? ToggleTaker(TaskItem t, string? name)
    {
        var n = CleanName(name);
        if (n.Length == 0) return null;
        var takers = t.Takers.ToList();
        var i = takers.FindIndex(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) takers.RemoveAt(i); else takers.Add(n);
        return t with { Takers = takers };
    }

    public static bool HasTaker(TaskItem t, string? name) =>
        t.Takers.Contains(CleanName(name), StringComparer.OrdinalIgnoreCase);

    public static TaskItem Copy(TaskItem t) => t with { Takers = t.Takers.ToList() };

    // 2026-11-08 user: "reminders and hour before tasks", "half an hour" - a push 60 and 30 minutes before a task's clock time.
    public static readonly int[] RemindMinutesBefore = [60, 30];
    // A reminder may still go out this long after its moment (the check runs every minute; a restart must not lose it).
    public static readonly TimeSpan RemindWindow = TimeSpan.FromMinutes(10);

    public static TimeOnly? Clock(TaskItem t) =>
        TimeOnly.TryParseExact(t.When, "HH:mm", out var c) ? c : null;

    // A yearly day without a clock time (a birthday) gets one push in the morning: "I dag: ...".
    public static readonly TimeOnly MorningPush = new(8, 0);

    /// <summary>Which reminders (minutes before) are due now, for a task that is on today's list and not done today.
    /// 0 = the morning push of a yearly day without a clock time.</summary>
    public static IEnumerable<int> DueReminders(TaskItem t, DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        if (!IsOn(t, today) || IsDoneOn(t, today)) yield break;
        if (Clock(t) is not { } c)
        {
            var morning = today.ToDateTime(MorningPush);
            if (t.Yearly && nowLocal >= morning && nowLocal < morning + RemindWindow) yield return 0;
            yield break;
        }
        var at = today.ToDateTime(c);
        foreach (var m in RemindMinutesBefore)
        {
            var moment = at.AddMinutes(-m);
            if (nowLocal >= moment && nowLocal < moment + RemindWindow) yield return m;
        }
    }

    public static string ReminderText(TaskItem t, int minutesBefore) =>
        minutesBefore == 0 ? $"I dag: {t.Title}"
        : (minutesBefore >= 60 ? "Om en time" : $"Om {minutesBefore} minutter") + $" (kl. {t.When}): {t.Title}"
          + (t.Takers.Count > 0 ? " – " + string.Join(", ", t.Takers) : " – mangler nogen");
}

// Who may do what: the group code makes a member, the admin code (Martin) also makes, edits and removes tasks.
public static class AccessRules
{
    public const int MinGroupCode = 4, MinAdminCode = 6, MaxWrongTries = 10;
    public static readonly TimeSpan WrongTriesWindow = TimeSpan.FromMinutes(15);

    /// <summary>The cookie value: a hash, so the code itself never sits in the browser.</summary>
    public static string Token(string secret, string kind) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + ":" + secret)));
}
