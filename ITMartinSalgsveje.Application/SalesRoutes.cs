using ITMartinSalgsveje.Domain;

namespace ITMartinSalgsveje.Application;

public sealed record ChannelSaveResult(bool Saved, List<string> Problems, Guid Id);

/// <summary>A channel with its open and done tasks (Hvem/Hvornår).</summary>
public sealed record ChannelCard(SalesChannel Channel, List<Assignment> Open, List<Assignment> Done);

/// <summary>
/// Salgsveje: every way the team sells, how it works, who does what and when, and what it gave.
/// Each channel owns one hovedopgave, so its tasks also show on Opgaver and in everyone's own list.
/// </summary>
public sealed class SalesRoutes(ITeamItems<SalesChannel> channels, ITeamItems<MainTask> mainTasks,
    ITeamItems<Assignment> tasks, IPushNotifier push, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<List<ChannelCard>> CardsAsync(Guid groupId, CancellationToken ct = default)
    {
        var all = await channels.ListAsync(groupId, ct);
        var allTasks = await tasks.ListAsync(groupId, ct);
        return SalesChannelRules.InOrder(all).Select(c => Card(c, allTasks)).ToList();
    }

    public async Task<ChannelCard?> CardAsync(Guid groupId, Guid id, CancellationToken ct = default)
    {
        var c = await channels.GetAsync(groupId, id, ct);
        return c is null ? null : Card(c, await tasks.ListAsync(groupId, ct));
    }

    private static ChannelCard Card(SalesChannel c, List<Assignment> allTasks)
    {
        var mine = c.MainTaskId is { } mt ? allTasks.Where(t => t.MainTaskId == mt).ToList() : [];
        return new(c,
            mine.Where(t => !t.IsCompleted).OrderBy(t => t.DueDate ?? DateTime.MaxValue).ToList(),
            mine.Where(t => t.IsCompleted).OrderByDescending(t => t.CompletedAt).ToList());
    }

    /// <summary>New or changed channel. Makes (or renames) its hovedopgave so the tasks have a home.</summary>
    public async Task<ChannelSaveResult> SaveAsync(Member by, SalesChannel c, CancellationToken ct = default)
    {
        if (by.GroupId != c.GroupId) return new(false, ["Du hører ikke til dette hold."], c.Id);
        c.Name = c.Name.Trim();
        c.Link = c.Link.Trim();
        var problems = SalesChannelRules.Problems(c);
        if (problems.Count > 0) return new(false, problems, c.Id);

        var all = await mainTasks.ListAsync(c.GroupId, ct);
        var title = $"Salgsvej: {c.Name}";
        if (c.MainTaskId is { } mtId && all.FirstOrDefault(m => m.Id == mtId) is { } mt)
        {
            if (mt.Title != title) { mt.Title = title; await mainTasks.UpdateAsync(mt, ct); }
        }
        else
        {
            var made = new MainTask { GroupId = c.GroupId, Title = title, SortOrder = all.Count };
            await mainTasks.AddAsync(made, ct);
            c.MainTaskId = made.Id;
        }

        c.UpdatedAt = Now;
        if (await channels.GetAsync(c.GroupId, c.Id, ct) is null) await channels.AddAsync(c, ct);
        else await channels.UpdateAsync(c, ct);
        return new(true, [], c.Id);
    }

    /// <summary>Only admins remove a channel. Its tasks stay on Opgaver (without the hovedopgave).</summary>
    public async Task<bool> DeleteAsync(Member by, Guid id, CancellationToken ct = default)
    {
        if (!by.IsAdmin) return false;
        var c = await channels.GetAsync(by.GroupId, id, ct);
        if (c is null) return false;
        await channels.DeleteAsync(by.GroupId, id, ct);
        return true;
    }

    /// <summary>Hvem + hvornår: a task under the channel. Who = "" means anyone may take it.</summary>
    public async Task<List<string>> AddTaskAsync(Member by, Guid channelId, string title, string who, DateTime? due, CancellationToken ct = default)
    {
        if (title.Trim().Length == 0) return ["Skriv hvad der skal gøres, fx \"Book stand\"."];
        var c = await channels.GetAsync(by.GroupId, channelId, ct);
        if (c?.MainTaskId is null) return ["Salgsvejen findes ikke længere."];
        var a = new Assignment
        {
            GroupId = by.GroupId, MainTaskId = c.MainTaskId, Title = title.Trim(), DueDate = due,
            CreatedByName = by.Name, CreatedAt = Now,
        };
        if (who.Trim().Length > 0) a.AddAssignee(who.Trim());
        await tasks.AddAsync(a, ct);
        var tell = a.PeopleToTell(by.Name);
        if (tell.Count > 0) _ = push.ToMembersAsync(by.GroupId, tell, "Ny opgave", $"{c.Name}: {a.Title}");
        return [];
    }

    public async Task CompleteTaskAsync(Member by, Guid taskId, CancellationToken ct = default)
    {
        var t = await tasks.GetAsync(by.GroupId, taskId, ct);
        if (t is null || t.IsCompleted) return;
        t.Complete(by.Name, Now);
        await tasks.UpdateAsync(t, ct);
    }

    public async Task TakeTaskAsync(Member by, Guid taskId, CancellationToken ct = default)
    {
        var t = await tasks.GetAsync(by.GroupId, taskId, ct);
        if (t is null) return;
        t.AddAssignee(by.Name);
        await tasks.UpdateAsync(t, ct);
    }

    /// <summary>The first cards (market search 2026-11-04) - only when the team has no channels yet.</summary>
    public async Task<int> AddStarterListAsync(Member by, CancellationToken ct = default)
    {
        if ((await channels.ListAsync(by.GroupId, ct)).Count > 0) return 0;
        var n = 0;
        foreach (var c in StarterList.For(by.GroupId))
            if ((await SaveAsync(by, c, ct)).Saved) n++;
        return n;
    }
}
