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
    // earlier day reopens automatically so it can be done again today.
    // Extracted from GroupHome.razor's RefreshAsync 2026-09-06 - previously
    // untestable page-private logic, now a plain data operation.
    public async Task ReopenStaleDailyTasksAsync(Guid groupId, IReadOnlyList<Guid> dailyMainTaskIds, DateTime localTodayStartUtc)
    {
        if (dailyMainTaskIds.Count == 0) return;

        var staleDaily = await db.Assignments
            .Where(a => a.GroupId == groupId && a.IsCompleted && a.MainTaskId != null
                && dailyMainTaskIds.Contains(a.MainTaskId!.Value)
                && a.CompletedAt!.Value < localTodayStartUtc)
            .ToListAsync();
        if (staleDaily.Count == 0) return;

        foreach (var a in staleDaily) { a.IsCompleted = false; a.CompletedAt = null; a.CompletedByName = ""; }
        await db.SaveChangesAsync();
    }

    // "Done" for a daily main task means all of today's subtasks are complete -
    // the open-task count alone can't show that, so the caller needs today's
    // completed count alongside it.
    public async Task<Dictionary<Guid, int>> GetDailyDoneCountsAsync(Guid groupId, IReadOnlyList<Guid> dailyMainTaskIds)
    {
        if (dailyMainTaskIds.Count == 0) return [];

        var counts = await db.Assignments
            .Where(a => a.GroupId == groupId && a.IsCompleted && a.MainTaskId != null
                && dailyMainTaskIds.Contains(a.MainTaskId!.Value))
            .GroupBy(a => a.MainTaskId!.Value)
            .Select(g => new { MainTaskId = g.Key, Count = g.Count() })
            .ToListAsync();
        return counts.ToDictionary(x => x.MainTaskId, x => x.Count);
    }
}
