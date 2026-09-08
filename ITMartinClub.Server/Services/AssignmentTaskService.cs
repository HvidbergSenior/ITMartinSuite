using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Server.Services;

// Shared Assignment create/update/claim/complete/delete logic - GroupHome.razor
// (Overblik) and Assignments.razor (Historik) both offer the exact same task
// actions, and used to each keep their own copy. That let the Copenhagen-
// timezone day-boundary fix land in GroupHome only, silently missing
// Assignments' view of "done" - extracted 2026-09-06 so a future fix only
// has to happen once. Every method re-checks GroupId itself rather than
// trusting a bare Id from the caller.
public sealed class AssignmentTaskService(ClubDbContext db, ClubPushService pushService)
{
    public static string PrefixWithMainTask(string title, string mainTaskTitle)
    {
        var prefix = $"{mainTaskTitle}: ";
        return title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? title : prefix + title;
    }

    public static string StripMainTaskPrefix(string title, string mainTaskTitle)
    {
        var prefix = $"{mainTaskTitle}: ";
        return title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? title[prefix.Length..] : title;
    }

    public async Task SaveAsync(
        Guid groupId, Guid? editingId, Guid? mainTaskId, string? mainTaskTitle, Guid? storageLocationId,
        string title, string? description, List<string> assignedTo, DateTime? dueDateUtc, DateTime? scheduledForUtc,
        string actingMemberName)
    {
        var finalTitle = mainTaskId is not null && mainTaskTitle is not null
            ? PrefixWithMainTask(title, mainTaskTitle)
            : title;

        if (editingId is { } id)
        {
            var t = await db.Assignments.FirstOrDefaultAsync(a => a.Id == id && a.GroupId == groupId);
            if (t is null) return;
            t.MainTaskId = mainTaskId;
            t.StorageLocationId = storageLocationId;
            t.Title = finalTitle;
            t.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            t.AssignedToNames = string.Join(';', assignedTo);
            t.DueDate = dueDateUtc;
            t.ScheduledFor = scheduledForUtc;
            await db.SaveChangesAsync();

            var recipients = assignedTo.Append(t.CreatedByName)
                .Where(n => n != actingMemberName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (recipients.Count > 0)
                _ = pushService.SendToMembersAsync(db, groupId, recipients, "✏️ Opgave opdateret", $"{actingMemberName}: {finalTitle}");
            return;
        }

        db.Assignments.Add(new Assignment
        {
            GroupId = groupId,
            MainTaskId = mainTaskId,
            StorageLocationId = storageLocationId,
            Title = finalTitle,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            AssignedToNames = string.Join(';', assignedTo),
            DueDate = dueDateUtc,
            ScheduledFor = scheduledForUtc,
            CreatedByName = actingMemberName,
        });
        await db.SaveChangesAsync();

        // Only ping the people it actually concerns: the chosen assignee(s) if
        // any were picked, otherwise the whole group since it's up for grabs.
        var newTaskRecipients = assignedTo.Where(n => n != actingMemberName).ToList();
        if (newTaskRecipients.Count > 0)
            _ = pushService.SendToMembersAsync(db, groupId, newTaskRecipients, "✅ Ny opgave", $"{actingMemberName}: {finalTitle}");
        else if (assignedTo.Count == 0)
            _ = pushService.SendToGroupAsync(db, groupId, actingMemberName, "✅ Ny opgave", $"{actingMemberName}: {finalTitle}");
    }

    public async Task JoinAsync(Guid id, Guid groupId, string memberName)
    {
        var t = await db.Assignments.FirstOrDefaultAsync(a => a.Id == id && a.GroupId == groupId);
        if (t is null) return;
        var priorAssignees = t.Assignees;
        t.AddAssignee(memberName);
        await db.SaveChangesAsync();

        var recipients = priorAssignees.Append(t.CreatedByName)
            .Where(n => n != memberName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count > 0)
            _ = pushService.SendToMembersAsync(db, groupId, recipients, "👋 Opgave taget", $"{memberName} tager: {t.Title}");
    }

    // "Jeg kan ikke" - the opposite of JoinAsync. Doesn't delete or complete
    // the task, just drops this member back to the unclaimed pool so someone
    // else can pick it up.
    public async Task DeclineAsync(Guid id, Guid groupId, string memberName)
    {
        var t = await db.Assignments.FirstOrDefaultAsync(a => a.Id == id && a.GroupId == groupId);
        if (t is null) return;
        t.RemoveAssignee(memberName);
        await db.SaveChangesAsync();

        if (t.CreatedByName != memberName)
            _ = pushService.SendToMembersAsync(db, groupId, [t.CreatedByName], "🙅 Opgave afvist", $"{memberName} kan ikke: {t.Title}");
    }

    public async Task CompleteAsync(Guid id, Guid groupId, string memberName)
    {
        var t = await db.Assignments.FirstOrDefaultAsync(a => a.Id == id && a.GroupId == groupId);
        if (t is null) return;
        t.IsCompleted = true; t.CompletedAt = DateTime.UtcNow; t.CompletedByName = memberName;
        await db.SaveChangesAsync();

        var recipients = t.Assignees.Append(t.CreatedByName)
            .Where(n => n != memberName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count > 0)
            _ = pushService.SendToMembersAsync(db, groupId, recipients, "✅ Opgave færdig", $"{memberName}: {t.Title}");
    }

    public async Task DeleteAsync(Guid id, Guid groupId)
    {
        var t = await db.Assignments.FirstOrDefaultAsync(a => a.Id == id && a.GroupId == groupId);
        if (t is not null) { db.Assignments.Remove(t); await db.SaveChangesAsync(); }
    }

    public async Task AssignMainTaskAsync(Guid taskId, Guid groupId, Guid mainTaskId)
    {
        var t = await db.Assignments.FirstOrDefaultAsync(a => a.Id == taskId && a.GroupId == groupId);
        if (t is null) return;
        t.MainTaskId = mainTaskId;
        await db.SaveChangesAsync();
    }

    // A fixed Monday used only to number calendar weeks consecutively so
    // biweekly-with-days can tell "this week" from "the other week" the same
    // way for every task in the group, instead of each task having its own
    // independent 14-day clock from whenever it happened to be created.
    private static readonly DateTime BiweeklyEpochMonday = new(2024, 1, 1);

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
    // Extracted from GroupHome.razor's RefreshAsync 2026-09-06 - previously
    // untestable page-private logic, now a plain data operation. Generalized
    // 2026-09-08 to cover weekly recurrence alongside daily, then multi-day
    // weekly, then monthly/biweekly, then day-pickers for monthly/biweekly
    // too, all the same day. Takes the MainTask rows themselves (rather than
    // separate id lists/dictionaries per kind) so a future recurrence
    // variation doesn't mean another parameter.
    public async Task ReopenStaleTasksAsync(
        Guid groupId,
        IReadOnlyList<MainTask> recurringMainTasks,
        DateTime localTodayStartUtc)
    {
        if (recurringMainTasks.Count == 0) return;

        // Per-mainTask staleness threshold - see the kind-by-kind rules in
        // the doc comment above. Whole-day offsets off an already-correct
        // local midnight, so this can be off by an hour across a DST
        // transition within the lookback window - an acceptable edge case
        // for a household chore reopening a bit early.
        var startOfMonthUtc = localTodayStartUtc.AddDays(1 - localTodayStartUtc.Day);
        var thresholds = new Dictionary<Guid, DateTime>();
        foreach (var mt in recurringMainTasks)
        {
            var days = mt.RecurrenceDays;
            if (mt.IsDaily)
            {
                thresholds[mt.Id] = localTodayStartUtc;
            }
            else if (mt.IsMonthly)
            {
                thresholds[mt.Id] = days.Count == 0
                    ? startOfMonthUtc
                    : days.Select(day => MonthlyDayThreshold(startOfMonthUtc, localTodayStartUtc, day)).Max();
            }
            else if (mt.IsBiweekly)
            {
                thresholds[mt.Id] = days.Count == 0
                    ? localTodayStartUtc.AddDays(-14)
                    : days.Select(day => MostRecentActiveBiweeklyOccurrence(localTodayStartUtc, day)).Max();
            }
            else if (mt.IsWeekly && days.Count > 0)
            {
                thresholds[mt.Id] = days
                    .Select(day => localTodayStartUtc.AddDays(-(((int)localTodayStartUtc.DayOfWeek - (int)day + 7) % 7)))
                    .Max();
            }
        }
        if (thresholds.Count == 0) return;

        var recurringIds = thresholds.Keys.ToList();
        var candidates = await db.Assignments
            .Where(a => a.GroupId == groupId && a.IsCompleted && a.MainTaskId != null
                && recurringIds.Contains(a.MainTaskId!.Value))
            .ToListAsync();

        var stale = candidates.Where(a => a.CompletedAt!.Value < thresholds[a.MainTaskId!.Value]).ToList();
        if (stale.Count == 0) return;

        foreach (var a in stale) { a.IsCompleted = false; a.CompletedAt = null; a.CompletedByName = ""; }
        await db.SaveChangesAsync();
    }

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

    // "Done" for a daily/weekly main task means all of its current cycle's
    // subtasks are complete - the open-task count alone can't show that, so
    // the caller needs the cycle's completed count alongside it.
    public async Task<Dictionary<Guid, int>> GetDailyDoneCountsAsync(Guid groupId, IReadOnlyList<Guid> recurringMainTaskIds)
    {
        if (recurringMainTaskIds.Count == 0) return [];

        var counts = await db.Assignments
            .Where(a => a.GroupId == groupId && a.IsCompleted && a.MainTaskId != null
                && recurringMainTaskIds.Contains(a.MainTaskId!.Value))
            .GroupBy(a => a.MainTaskId!.Value)
            .Select(g => new { MainTaskId = g.Key, Count = g.Count() })
            .ToListAsync();
        return counts.ToDictionary(x => x.MainTaskId, x => x.Count);
    }
}
