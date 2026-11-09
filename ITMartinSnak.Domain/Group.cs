using System.Security.Cryptography;
using System.Text;

namespace ITMartinSnak.Domain;

// The shapes are kept exactly as they were stored in messages.json / tasks.json since 2026-10-09 - existing files read as before.
// Host = written by Martin (the room owner) - the overview shows who is waiting for an answer (2026-11-04).
public sealed record Message(long Id, string Name, string Text, DateTime At, bool Host = false);

// 2026-11-08 TODAY (user: "combine with today. So all people can see what happens today and today only"): Day = the date it is
// for ("yyyy-MM-dd", "" = no day), Daily = every day (medicine, a phone call) - its tick (DoneOn) only counts for that day.
public sealed record TaskItem(long Id, string Title, string Note, string When, List<string> Takers, bool Done, DateTime CreatedAt,
    string Day = "", bool Daily = false, string DoneOn = "");

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
    public static TaskItem? NewTask(IReadOnlyList<TaskItem> existing, string? title, string? note, string? when, DateTime nowUtc, string? to = null,
        string? day = null, bool daily = false)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        var n = CleanName(to);
        return new TaskItem((existing.Count == 0 ? 0 : existing.Max(x => x.Id)) + 1, Cut(ti, MaxTitle), Cut((note ?? "").Trim(), MaxNote),
            Cut((when ?? "").Trim(), MaxWhen), n.Length > 0 ? [n] : [], false, nowUtc, daily ? "" : CleanDay(day), daily);
    }

    /// <summary>Only a real date "yyyy-MM-dd" is kept.</summary>
    public static string CleanDay(string? day) =>
        DateOnly.TryParseExact((day ?? "").Trim(), "yyyy-MM-dd", out var d) ? d.ToString("yyyy-MM-dd") : "";

    /// <summary>Ticked off: a daily task only for today (it comes back tomorrow), any other task for good.</summary>
    public static TaskItem SetDone(TaskItem t, bool done, DateOnly today) =>
        t.Daily ? t with { Done = false, DoneOn = done ? today.ToString("yyyy-MM-dd") : "" } : t with { Done = done };

    /// <summary>What "I dag" shows: daily tasks, today's, and the ones with no day that are not done.</summary>
    public static bool IsToday(TaskItem t, DateOnly today) =>
        t.Daily || t.Day == today.ToString("yyyy-MM-dd") || (t.Day.Length == 0 && !t.Done);

    /// <param name="to">null = keep who has it; "" = nobody; a name = give it to that person.</param>
    public static TaskItem? Edit(TaskItem t, string? title, string? note, string? when, string? to = null, string? day = null, bool? daily = null)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        var takers = to is null ? t.Takers.ToList() : CleanName(to) is { Length: > 0 } n ? [n] : new List<string>();
        var d = daily ?? t.Daily;
        return t with
        {
            Title = Cut(ti, MaxTitle), Note = Cut((note ?? "").Trim(), MaxNote), When = Cut((when ?? "").Trim(), MaxWhen), Takers = takers,
            Daily = d, Day = d ? "" : day is null ? t.Day : CleanDay(day), DoneOn = d ? t.DoneOn : "",
        };
    }

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

    /// <summary>Which reminders (minutes before) are due now, for a task that is on today's list and not done today.</summary>
    public static IEnumerable<int> DueReminders(TaskItem t, DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        if (Clock(t) is not { } c || !IsToday(t, today) || t.Day.Length == 0 && !t.Daily) yield break;
        if (t.Daily ? t.DoneOn == today.ToString("yyyy-MM-dd") : t.Done) yield break;
        var at = today.ToDateTime(c);
        foreach (var m in RemindMinutesBefore)
        {
            var moment = at.AddMinutes(-m);
            if (nowLocal >= moment && nowLocal < moment + RemindWindow) yield return m;
        }
    }

    public static string ReminderText(TaskItem t, int minutesBefore) =>
        (minutesBefore >= 60 ? "Om en time" : $"Om {minutesBefore} minutter") + $" (kl. {t.When}): {t.Title}"
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
