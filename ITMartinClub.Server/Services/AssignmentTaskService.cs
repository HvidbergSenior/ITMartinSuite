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

    // Daily main tasks are a recurring checklist: a subtask completed on an
    // earlier day reopens automatically so it can be done again today. Weekly
    // main tasks work the same way but only reopen once a new occurrence of
    // their specific weekday arrives (e.g. "every Thursday: clean the
    // bathroom" stays done from Thursday through next Wednesday).
    // Extracted from GroupHome.razor's RefreshAsync 2026-09-06 - previously
    // untestable page-private logic, now a plain data operation. Generalized
    // 2026-09-08 to cover weekly recurrence alongside daily.
    public async Task ReopenStaleTasksAsync(
        Guid groupId,
        IReadOnlyList<Guid> dailyMainTaskIds,
        IReadOnlyDictionary<Guid, DayOfWeek> weeklyMainTasks,
        DateTime localTodayStartUtc)
    {
        if (dailyMainTaskIds.Count == 0 && weeklyMainTasks.Count == 0) return;

        // Per-mainTask staleness threshold: daily tasks use today's local
        // midnight; weekly tasks use the most recent local midnight that fell
        // on their recurrence weekday (today counts if it matches). Whole-day
        // offsets off an already-correct local midnight, so this can be off
        // by an hour across a DST transition within the lookback week - an
        // acceptable edge case for a household chore reopening a day early.
        var thresholds = new Dictionary<Guid, DateTime>();
        foreach (var id in dailyMainTaskIds) thresholds[id] = localTodayStartUtc;
        foreach (var (id, day) in weeklyMainTasks)
        {
            var daysSinceLastOccurrence = ((int)localTodayStartUtc.DayOfWeek - (int)day + 7) % 7;
            thresholds[id] = localTodayStartUtc.AddDays(-daysSinceLastOccurrence);
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
