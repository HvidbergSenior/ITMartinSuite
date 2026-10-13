using ITMartinClub.Domain;

namespace ITMartinClub.Application;

/// <summary>The front page board: hovedopgaver in order, tasks open now, tasks held for a later day, done counts per recurring hovedopgave.</summary>
public sealed record HomeBoard(List<MainTask> MainTasks, List<Assignment> OpenNow, List<Assignment> Later, Dictionary<Guid, int> DoneThisCycle);

// Tasks (Assignment) and hovedopgaver (MainTask). GroupHome (Overblik), Assignments (Historik), Opgaver (Bogshoppen) and
// Møder all use the same actions - one place, so a fix (like the Copenhagen day boundary) lands everywhere.
// Every method re-checks the team itself rather than trusting a bare Id from the caller.
public sealed class TaskBoard(ITeamItems<Assignment> tasks, ITeamItems<MainTask> mainTasks, ITaskStore store, IPushNotifier push, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<List<MainTask>> MainTasksAsync(Guid groupId) =>
        (await mainTasks.ListAsync(groupId)).OrderBy(m => m.SortOrder).ToList();

    public Task<List<Assignment>> AllAsync(Guid groupId) => tasks.ListAsync(groupId);

    public async Task<HomeBoard> HomeAsync(Group group)
    {
        if (FixedBuckets.IsUsedBy(group.Slug)) await EnsureFixedBucketsAsync(group.Id);
        var mains = await MainTasksAsync(group.Id);
        if (mains.Count == 0) return new HomeBoard(mains, [], [], []);

        var todayStart = DanishTime.TodayStartUtc(Now);
        var recurring = mains.Where(m => m.IsDaily || m.IsRecurringCard).ToList();
        await ReopenStaleAsync(group.Id, recurring, todayStart);

        var all = await tasks.ListAsync(group.Id);
        var open = all.Where(a => !a.IsCompleted).ToList();
        var recurringIds = recurring.Select(m => m.Id).ToHashSet();
        return new HomeBoard(
            mains,
            open.Where(a => a.ScheduledFor == null || a.ScheduledFor <= todayStart).OrderBy(a => a.DueDate).ToList(),
            open.Where(a => a.ScheduledFor != null && a.ScheduledFor > todayStart).OrderBy(a => a.ScheduledFor).ToList(),
            all.Where(a => a.IsCompleted && a.MainTaskId is { } id && recurringIds.Contains(id))
                .GroupBy(a => a.MainTaskId!.Value).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>Done tasks from an earlier cycle of their recurring hovedopgave come back as open.</summary>
    public async Task ReopenStaleAsync(Guid groupId, IReadOnlyList<MainTask> recurring, DateTime localTodayStartUtc)
    {
        var thresholds = TaskRecurrence.ReopenThresholds(recurring, localTodayStartUtc);
        if (thresholds.Count == 0) return;
        var stale = TaskRecurrence.Stale(await store.CompletedUnderAsync(groupId, thresholds.Keys.ToList()), thresholds);
        if (stale.Count == 0) return;
        foreach (var a in stale) a.Reopen();
        await tasks.UpdateAsync(stale);
    }

    // Creates the five columns if missing and folds every other hovedopgave into the column matching its old
    // recurrence (open and completed tasks alike, so history stays intact), then removes it. Idempotent.
    private async Task EnsureFixedBucketsAsync(Guid groupId)
    {
        var existing = await mainTasks.ListAsync(groupId);
        List<MainTask> added = [], changed = [], buckets = [];
        for (var i = 0; i < FixedBuckets.All.Length; i++)
        {
            var b = FixedBuckets.All[i];
            var mt = existing.FirstOrDefault(m => m.Title == b.Title);
            if (mt is null)
            {
                mt = new MainTask { GroupId = groupId, Title = b.Title, IsDaily = b.Daily, RecurrenceDaysRaw = b.Days, IsBiweekly = b.Biweekly, IsMonthly = b.Monthly, SortOrder = i };
                added.Add(mt);
            }
            else if (mt.SortOrder != i || mt.IsDaily != b.Daily || mt.RecurrenceDaysRaw != b.Days || mt.IsBiweekly != b.Biweekly || mt.IsMonthly != b.Monthly)
            {
                mt.SortOrder = i; mt.IsDaily = b.Daily; mt.RecurrenceDaysRaw = b.Days; mt.IsBiweekly = b.Biweekly; mt.IsMonthly = b.Monthly;
                changed.Add(mt);
            }
            buckets.Add(mt);
        }
        var old = existing.Where(m => FixedBuckets.All.All(b => b.Title != m.Title)).ToList();
        if (added.Count == 0 && changed.Count == 0 && old.Count == 0) return;

        List<Assignment> moved = [];
        if (old.Count > 0)
        {
            var all = await tasks.ListAsync(groupId);
            foreach (var o in old)
                foreach (var a in all.Where(a => a.MainTaskId == o.Id))
                {
                    a.MainTaskId = buckets[FixedBuckets.TargetIndex(o)].Id;
                    // Keep the old chore's name visible on the task itself.
                    if (!a.Title.StartsWith(o.Title, StringComparison.OrdinalIgnoreCase)) a.Title = o.Title + " – " + a.Title;
                    moved.Add(a);
                }
        }
        await store.SaveLayoutAsync(groupId, added, changed, old.Select(o => o.Id), moved);
    }

    public async Task SaveAsync(
        Guid groupId, Guid? editingId, Guid? mainTaskId, string? mainTaskTitle, Guid? storageLocationId,
        string title, string? description, List<string> assignedTo, DateTime? dueDateUtc, DateTime? scheduledForUtc,
        string actingMemberName)
    {
        var finalTitle = mainTaskId is not null && mainTaskTitle is not null ? TaskTitles.WithMainTask(title, mainTaskTitle) : title;
        var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (editingId is { } id)
        {
            var t = await tasks.GetAsync(groupId, id);
            if (t is null) return;
            t.MainTaskId = mainTaskId;
            t.StorageLocationId = storageLocationId;
            t.Title = finalTitle;
            t.Description = desc;
            t.AssignedToNames = string.Join(';', assignedTo);
            t.DueDate = dueDateUtc;
            t.ScheduledFor = scheduledForUtc;
            await tasks.UpdateAsync(t);

            var recipients = t.PeopleToTell(actingMemberName, assignedTo);
            if (recipients.Count > 0)
                _ = push.ToMembersAsync(groupId, recipients, "✏️ Opgave opdateret", $"{actingMemberName}: {finalTitle}");
            return;
        }

        await tasks.AddAsync(new Assignment
        {
            GroupId = groupId, MainTaskId = mainTaskId, StorageLocationId = storageLocationId, Title = finalTitle,
            Description = desc, AssignedToNames = string.Join(';', assignedTo), DueDate = dueDateUtc, ScheduledFor = scheduledForUtc,
            CreatedByName = actingMemberName,
        });

        // Only ping the people it actually concerns: the chosen assignee(s) if any were picked, otherwise the whole
        // team since it's up for grabs.
        var newTaskRecipients = assignedTo.Where(n => n != actingMemberName).ToList();
        if (newTaskRecipients.Count > 0)
            _ = push.ToMembersAsync(groupId, newTaskRecipients, "✅ Ny opgave", $"{actingMemberName}: {finalTitle}");
        else if (assignedTo.Count == 0)
            _ = push.ToTeamAsync(groupId, actingMemberName, "✅ Ny opgave", $"{actingMemberName}: {finalTitle}");
    }

    /// <summary>Bogshoppen's quick add: a topic by name (made if new), at most one person, no push.</summary>
    public async Task QuickAddAsync(Guid groupId, string title, string topic, string who, DateTime? due, string actingMemberName)
    {
        var topicId = topic.Trim().Length > 0 ? await TopicIdAsync(groupId, topic.Trim()) : (Guid?)null;
        var a = new Assignment { GroupId = groupId, Title = title.Trim(), MainTaskId = topicId, DueDate = due, CreatedByName = actingMemberName };
        if (who.Length > 0) a.AddAssignee(who);
        await tasks.AddAsync(a);
    }

    /// <summary>The hovedopgave with this title - made (last in the list) if the team does not have it yet.</summary>
    public async Task<Guid> TopicIdAsync(Guid groupId, string title)
    {
        var all = await mainTasks.ListAsync(groupId);
        if (all.FirstOrDefault(t => t.Title == title) is { } found) return found.Id;
        var mt = new MainTask { GroupId = groupId, Title = title, SortOrder = all.Count };
        await mainTasks.AddAsync(mt);
        return mt.Id;
    }

    public async Task JoinAsync(Guid id, Guid groupId, string memberName, bool notify = true)
    {
        var t = await tasks.GetAsync(groupId, id);
        if (t is null) return;
        var priorAssignees = t.Assignees;
        t.AddAssignee(memberName);
        await tasks.UpdateAsync(t);

        var recipients = t.PeopleToTell(memberName, priorAssignees);
        if (notify && recipients.Count > 0)
            _ = push.ToMembersAsync(groupId, recipients, "👋 Opgave taget", $"{memberName} tager: {t.Title}");
    }

    // "Jeg kan ikke" - the opposite of JoinAsync. Doesn't delete or complete the task, just drops this member back
    // to the unclaimed pool so someone else can pick it up.
    public async Task DeclineAsync(Guid id, Guid groupId, string memberName)
    {
        var t = await tasks.GetAsync(groupId, id);
        if (t is null) return;
        t.RemoveAssignee(memberName);
        await tasks.UpdateAsync(t);

        if (t.CreatedByName != memberName)
            _ = push.ToMembersAsync(groupId, [t.CreatedByName], "🙅 Opgave afvist", $"{memberName} kan ikke: {t.Title}");
    }

    public async Task CompleteAsync(Guid id, Guid groupId, string memberName, bool notify = true)
    {
        var t = await tasks.GetAsync(groupId, id);
        if (t is null || t.IsCompleted) return;
        t.Complete(memberName, Now);
        await tasks.UpdateAsync(t);

        var recipients = t.PeopleToTell(memberName);
        if (notify && recipients.Count > 0)
            _ = push.ToMembersAsync(groupId, recipients, "✅ Opgave færdig", $"{memberName}: {t.Title}");
    }

    public Task DeleteAsync(Guid id, Guid groupId) => tasks.DeleteAsync(groupId, id);

    public async Task AssignMainTaskAsync(Guid taskId, Guid groupId, Guid mainTaskId)
    {
        var t = await tasks.GetAsync(groupId, taskId);
        if (t is null) return;
        t.MainTaskId = mainTaskId;
        await tasks.UpdateAsync(t);
    }

    public async Task AddMainTaskAsync(Guid groupId, string title, string recurrence, IEnumerable<DayOfWeek> days)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        var count = (await mainTasks.ListAsync(groupId)).Count;
        await mainTasks.AddAsync(MainTask.Create(groupId, title, count, recurrence, days));
    }

    public async Task RenameMainTaskAsync(Guid groupId, Guid id, string newTitle)
    {
        var title = newTitle.Trim();
        if (title.Length == 0) return;
        var mt = await mainTasks.GetAsync(groupId, id);
        if (mt is null) return;
        mt.Title = title;
        await mainTasks.UpdateAsync(mt);
    }

    // Tasks still under this hovedopgave are kept, not deleted - they drop to "Ikke kategoriseret" instead.
    public async Task DeleteMainTaskAsync(Group group, Guid id)
    {
        if (FixedBuckets.IsUsedBy(group.Slug)) return;   // the five columns are the structure, not content
        if (await mainTasks.GetAsync(group.Id, id) is null) return;
        await store.SaveLayoutAsync(group.Id, [], [], [id], []);
    }

    public Task<int> ClearCompletedAsync(Guid groupId) => store.DeleteCompletedAsync(groupId);

    /// <summary>How many open tasks each person has (Medlemmer).</summary>
    public async Task<Dictionary<string, int>> OpenCountByNameAsync(Guid groupId) =>
        (await tasks.ListAsync(groupId)).Where(a => !a.IsCompleted)
            .SelectMany(a => a.Assignees)
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Tasks tied to a place (Opbevaring): open first, then by due date.</summary>
    public async Task<Dictionary<Guid, List<Assignment>>> ByPlaceAsync(Guid groupId) =>
        (await tasks.ListAsync(groupId)).Where(a => a.StorageLocationId != null)
            .OrderBy(a => a.IsCompleted).ThenBy(a => a.DueDate)
            .GroupBy(a => a.StorageLocationId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
}
