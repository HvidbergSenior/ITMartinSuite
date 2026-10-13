using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ITMartinClub.Domain;

/// <summary>Personal link tokens (/l/{token} invitation, /m/{token} message link): 32 hex chars.</summary>
public static class Tokens
{
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public static class EmailAddress
{
    public static bool LooksLike(string? s) =>
        s is { Length: > 5 and < 200 } && Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}

public static class Weekdays
{
    public static readonly DayOfWeek[] InOrder =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static string Danish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mandag",
        DayOfWeek.Tuesday => "Tirsdag",
        DayOfWeek.Wednesday => "Onsdag",
        DayOfWeek.Thursday => "Torsdag",
        DayOfWeek.Friday => "Fredag",
        DayOfWeek.Saturday => "Lørdag",
        _ => "Søndag",
    };
}

// "Today" for the daily-task reopen/scheduling logic must be Danish local
// calendar time, not UTC - CompletedAt/ScheduledFor are stored as UTC
// instants, and comparing raw UTC .Date values against them rotated the
// day boundary to 01:00/02:00 local. That reopened (or hid) tasks hours
// early for anything completed/scheduled between local midnight and the
// UTC rollover, which looked like "marked done but shows not done".
public static class DanishTime
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static DateTime TodayStartUtc(DateTime utcNow) =>
        TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(utcNow, Copenhagen).Date, Copenhagen);

    /// <summary>"I dag" tasks are due tonight at midnight, Danish time.</summary>
    public static DateTime TonightUtc(DateTime utcNow) => TodayStartUtc(utcNow).AddDays(1).AddMinutes(-1);

    public static DateTime Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Copenhagen);
}

// Every subtask title starts with its main task's name, e.g. "Salg: Skriv
// RABAT på skilt" - matches how tasks were already being titled by hand
// before MainTask existed as a real field, so it stays readable in lists
// that show tasks without their group header.
public static class TaskTitles
{
    public static string WithMainTask(string title, string mainTaskTitle)
    {
        var prefix = $"{mainTaskTitle}: ";
        return title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? title : prefix + title;
    }

    public static string WithoutMainTask(string title, string mainTaskTitle)
    {
        var prefix = $"{mainTaskTitle}: ";
        return title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? title[prefix.Length..] : title;
    }
}

// Daily main tasks are a recurring checklist: a subtask completed on an
// earlier day reopens automatically so it can be done again today. Weekly
// main tasks work the same way but only reopen once a new occurrence of
// one of their recurrence days arrives (e.g. "every Monday and Thursday:
// bins out" stays done from whichever of those days it was completed on
// through the day before its next occurrence). Monthly reopens once a
// new calendar month starts, or - if specific days are set - on the
// first occurrence of one of those weekdays on/after the 1st. Biweekly
// with no days set is not calendar-aligned - it's a rolling 14 days from
// whenever it was actually completed; with days set, it reopens on the
// given weekday(s) but only every OTHER calendar week (see
// BiweeklyEpochMonday), same across every task rather than each task
// keeping its own clock from its own completion time.
public static class TaskRecurrence
{
    // A fixed Monday used only to number calendar weeks consecutively so
    // biweekly-with-days can tell "this week" from "the other week" the same
    // way for every task in the group, instead of each task having its own
    // independent 14-day clock from whenever it happened to be created.
    private static readonly DateTime BiweeklyEpochMonday = new(2024, 1, 1);

    /// <summary>
    /// Per recurring main task: a subtask completed before this instant belongs to an earlier cycle and reopens.
    /// Whole-day offsets off an already-correct local midnight, so this can be off by an hour across a DST
    /// transition within the lookback window - an acceptable edge case for a household chore reopening a bit early.
    /// </summary>
    public static Dictionary<Guid, DateTime> ReopenThresholds(IEnumerable<MainTask> mainTasks, DateTime localTodayStartUtc)
    {
        var startOfMonthUtc = localTodayStartUtc.AddDays(1 - localTodayStartUtc.Day);
        var thresholds = new Dictionary<Guid, DateTime>();
        foreach (var mt in mainTasks)
        {
            var days = mt.RecurrenceDays;
            if (mt.IsDaily)
                thresholds[mt.Id] = localTodayStartUtc;
            else if (mt.IsMonthly)
                thresholds[mt.Id] = days.Count == 0
                    ? startOfMonthUtc
                    : days.Select(day => MonthlyDayThreshold(startOfMonthUtc, localTodayStartUtc, day)).Max();
            else if (mt.IsBiweekly)
                thresholds[mt.Id] = days.Count == 0
                    ? localTodayStartUtc.AddDays(-14)
                    : days.Select(day => MostRecentActiveBiweeklyOccurrence(localTodayStartUtc, day)).Max();
            else if (mt.IsWeekly && days.Count > 0)
                thresholds[mt.Id] = days
                    .Select(day => localTodayStartUtc.AddDays(-(((int)localTodayStartUtc.DayOfWeek - (int)day + 7) % 7)))
                    .Max();
        }
        return thresholds;
    }

    /// <summary>The completed tasks among these that belong to an earlier cycle of their main task.</summary>
    public static List<Assignment> Stale(IEnumerable<Assignment> completed, IReadOnlyDictionary<Guid, DateTime> thresholds) =>
        completed.Where(a => a.IsCompleted && a.CompletedAt is { } at && a.MainTaskId is { } mt
                             && thresholds.TryGetValue(mt, out var t) && at < t).ToList();

    // "First occurrence of `day` on/after the 1st of the month" - but only
    // once that occurrence has actually arrived. Before it arrives (e.g.
    // checking on the 1st itself, when the first Wednesday is still two days
    // off), the relevant boundary is still last month's occurrence of that
    // day, so whatever was completed against that stays valid a little
    // longer instead of getting reopened before its own day has even come
    // around this month.
    private static DateTime MonthlyDayThreshold(DateTime startOfMonthUtc, DateTime localTodayStartUtc, DayOfWeek day)
    {
        var firstOccurrenceThisMonth = startOfMonthUtc.AddDays(((int)day - (int)startOfMonthUtc.DayOfWeek + 7) % 7);
        if (firstOccurrenceThisMonth <= localTodayStartUtc) return firstOccurrenceThisMonth;

        var startOfPrevMonth = startOfMonthUtc.AddMonths(-1);
        return startOfPrevMonth.AddDays(((int)day - (int)startOfPrevMonth.DayOfWeek + 7) % 7);
    }

    // Most recent occurrence of `day` on/before `localTodayStartUtc` that
    // falls in an "active" (every-other) calendar week per BiweeklyEpochMonday
    // - if the plain most-recent occurrence landed in the other week, the
    // real most-recent active one was 7 days earlier still.
    private static DateTime MostRecentActiveBiweeklyOccurrence(DateTime localTodayStartUtc, DayOfWeek day)
    {
        var mostRecent = localTodayStartUtc.AddDays(-(((int)localTodayStartUtc.DayOfWeek - (int)day + 7) % 7));
        var weekIndex = (int)Math.Floor((mostRecent.Date - BiweeklyEpochMonday.Date).TotalDays / 7);
        return weekIndex % 2 == 0 ? mostRecent : mostRecent.AddDays(-7);
    }

    public static string Label(MainTask mt)
    {
        var days = mt.RecurrenceDays.Count > 0 ? string.Join(" + ", mt.RecurrenceDays.Select(Weekdays.Danish)) : null;
        if (mt.IsMonthly) return days is null ? "Månedligt" : $"Månedligt ({days})";
        if (mt.IsBiweekly) return days is null ? "Hver 14. dag" : $"Hver 14. dag ({days})";
        return days ?? "";
    }
}

// Familien Hvidberg (2026-09-19): exactly five fixed hovedopgaver as
// columns - I dag (due tonight), Dagligt, Ugentligt, Hver 14. dag,
// Månedligt. Tasks are added straight into a column, never with a
// recurrence of their own; the column decides when they reopen.
public static class FixedBuckets
{
    public sealed record Bucket(string Title, bool Daily, string Days, bool Biweekly, bool Monthly);

    public static readonly Bucket[] All =
    [
        new("☀️ I dag", false, "", false, false),
        new("🔁 Dagligt", true, "", false, false),
        new("📅 Ugentligt", false, "Monday", false, false),
        new("🗓️ Hver 14. dag", false, "", true, false),
        new("🌙 Månedligt", false, "", false, true),
    ];

    public static string TodayTitle => All[0].Title;

    public static bool IsUsedBy(string slug) => slug == "hvidberg";

    /// <summary>The column an old hovedopgave folds into, by its old recurrence.</summary>
    public static int TargetIndex(MainTask old) =>
        old.IsDaily ? 1 : old.IsBiweekly ? 3 : old.IsMonthly ? 4 : old.IsWeekly ? 2 : 0;
}
