using ITMartinClub.Application;
using ITMartinClub.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Infrastructure.Persistence;

// EF Core + SQLite (club.db). A short-lived DbContext per call - Blazor Server keeps components alive for minutes, so a
// long-lived context would hold stale rows, and fire-and-forget pushes used to share the page's context.
// Writes of detached rows mark only those rows (SaveRowsAsync) - never the navigation graph loaded with them
// (Member.Group from the session would otherwise be inserted again).

internal static class RowWrites
{
    public static async Task SaveRowsAsync(this ClubDb db, IEnumerable<object> rows, EntityState state, CancellationToken ct)
    {
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var row in rows) db.Entry(row).State = state;
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class TeamItems<T>(IDbContextFactory<ClubDb> dbf) : ITeamItems<T> where T : class, ITeamOwned
{
    public async Task<List<T>> ListAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Set<T>().AsNoTracking().Where(x => x.GroupId == groupId).ToListAsync(ct);
    }

    public async Task<T?> GetAsync(Guid groupId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.GroupId == groupId, ct);
    }

    public async Task AddAsync(T item, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync([item], EntityState.Added, ct);
    }

    public Task UpdateAsync(T item, CancellationToken ct = default) => UpdateAsync([item], ct);

    public async Task UpdateAsync(IEnumerable<T> items, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync(items, EntityState.Modified, ct);
    }

    public async Task DeleteAsync(Guid groupId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Set<T>().Where(x => x.Id == id && x.GroupId == groupId).ExecuteDeleteAsync(ct);
    }
}

internal sealed class TeamStore(IDbContextFactory<ClubDb> dbf) : ITeamStore
{
    public async Task<Group?> GroupAsync(string slug, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Slug == slug, ct);
    }

    public async Task<Group?> GroupAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task AddGroupAsync(Group group, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Member>> MembersAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Members.AsNoTracking().Where(m => m.GroupId == groupId).OrderBy(m => m.Name).ToListAsync(ct);
    }

    public async Task<Member?> MemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == groupId, ct);
    }

    public async Task<Member?> MemberByNameAsync(Guid groupId, string name, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.GroupId == groupId && m.Name == name, ct);
    }

    public async Task<Member?> MemberByLoginTokenAsync(string token, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Members.AsNoTracking().Include(m => m.Group).FirstOrDefaultAsync(m => m.LoginToken == token, ct);
    }

    public async Task<Member?> FirstWithRoleAsync(string slug, string role, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Members.AsNoTracking().Include(m => m.Group)
            .Where(m => m.Group.Slug == slug && m.Role == role).OrderBy(m => m.Name).FirstOrDefaultAsync(ct);
    }

    public async Task AddMemberAsync(Member member, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync([member], EntityState.Added, ct);
    }

    public async Task UpdateMemberAsync(Member member, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync([member], EntityState.Modified, ct);
    }

    public async Task RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var member = await db.Members.Include(m => m.Sessions).Include(m => m.Posts)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == groupId, ct);
        if (member is null) return;
        db.Members.Remove(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<MemberSession>> SessionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Sessions.AsNoTracking().Include(s => s.Member).ThenInclude(m => m.Group)
            .Where(s => ids.Contains(s.Id)).ToListAsync(ct);
    }

    public async Task AddSessionAsync(MemberSession session, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync([session], EntityState.Added, ct);   // only the session row - Member may be set for the caller
    }

    public async Task SetSessionExpiryAsync(Guid sessionId, DateTime expiresAt, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Sessions.Where(s => s.Id == sessionId).ExecuteUpdateAsync(u => u.SetProperty(s => s.ExpiresAt, expiresAt), ct);
    }
}

internal sealed class MessageStore(IDbContextFactory<ClubDb> dbf) : IMessageStore
{
    public async Task AddAsync(ClubMessage message, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Messages.Add(message);   // with its receipts
        await db.SaveChangesAsync(ct);
    }

    public async Task<ClubMessage?> GetAsync(Guid groupId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Messages.AsNoTracking().Include(m => m.Receipts).FirstOrDefaultAsync(m => m.Id == id && m.GroupId == groupId, ct);
    }

    public async Task<List<ClubMessage>> ForMemberAsync(Guid groupId, Guid memberId, int max, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Messages.AsNoTracking().Include(m => m.Receipts)
            .Where(m => m.GroupId == groupId && (m.FromMemberId == memberId || m.Receipts.Any(r => r.MemberId == memberId)))
            .OrderByDescending(m => m.CreatedAt).Take(max).ToListAsync(ct);
    }

    public async Task<MessageReceipt?> ReceiptByTokenAsync(string token, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Receipts.AsNoTracking().Include(r => r.Message).FirstOrDefaultAsync(r => r.Token == token, ct);
    }

    public async Task UpdateReceiptsAsync(IEnumerable<MessageReceipt> receipts, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync(receipts, EntityState.Modified, ct);
    }

    public async Task<List<MessageReceipt>> AwaitingReminderAsync(DateTime sentFrom, DateTime sentTo, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Receipts.AsNoTracking().Include(r => r.Message)
            .Where(r => r.Message.RequireConfirm && r.ConfirmedAt == null && r.RemindedAt == null
                        && r.Message.CreatedAt < sentTo && r.Message.CreatedAt > sentFrom)
            .ToListAsync(ct);
    }
}

internal sealed class TaskStore(IDbContextFactory<ClubDb> dbf) : ITaskStore
{
    public async Task<List<Assignment>> CompletedUnderAsync(Guid groupId, IReadOnlyCollection<Guid> mainTaskIds, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Assignments.AsNoTracking()
            .Where(a => a.GroupId == groupId && a.IsCompleted && a.MainTaskId != null && mainTaskIds.Contains(a.MainTaskId!.Value))
            .ToListAsync(ct);
    }

    public async Task<int> DeleteCompletedAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Assignments.Where(a => a.GroupId == groupId && a.IsCompleted).ExecuteDeleteAsync(ct);
    }

    public async Task SaveLayoutAsync(Guid groupId, IEnumerable<MainTask> added, IEnumerable<MainTask> changed, IEnumerable<Guid> removed,
        IEnumerable<Assignment> movedTasks, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var mt in added) db.Entry(mt).State = EntityState.Added;
        foreach (var mt in changed) db.Entry(mt).State = EntityState.Modified;
        foreach (var a in movedTasks) db.Entry(a).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);

        var gone = removed.ToList();
        if (gone.Count == 0) return;
        await db.Assignments.Where(a => a.GroupId == groupId && a.MainTaskId != null && gone.Contains(a.MainTaskId!.Value))
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.MainTaskId, (Guid?)null), ct);
        await db.MainTasks.Where(m => m.GroupId == groupId && gone.Contains(m.Id)).ExecuteDeleteAsync(ct);
    }
}

internal sealed class CalendarStore(IDbContextFactory<ClubDb> dbf) : ICalendarStore
{
    public async Task<List<EventRsvp>> RsvpsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.EventRsvps.AsNoTracking().Where(r => eventIds.Contains(r.EventId)).ToListAsync(ct);
    }

    public async Task<List<EventPrep>> PrepsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.EventPreps.AsNoTracking().Where(p => eventIds.Contains(p.EventId)).OrderBy(p => p.CreatedAt).ToListAsync(ct);
    }

    public async Task<List<EventTimeSuggestion>> OpenSuggestionsAsync(IReadOnlyCollection<Guid> eventIds, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.EventTimeSuggestions.AsNoTracking()
            .Where(s => eventIds.Contains(s.EventId) && !s.Resolved && s.ExpiresAt > nowUtc).ToListAsync(ct);
    }

    public async Task<List<EventTimeVote>> VotesAsync(IReadOnlyCollection<Guid> suggestionIds, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.EventTimeVotes.AsNoTracking().Where(v => suggestionIds.Contains(v.SuggestionId)).ToListAsync(ct);
    }

    public async Task SetRsvpAsync(Guid eventId, string memberName, string status, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var existing = await db.EventRsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.MemberName == memberName, ct);
        if (existing is not null) { existing.Status = status; existing.RespondedAt = nowUtc; }
        else db.EventRsvps.Add(new EventRsvp { EventId = eventId, MemberName = memberName, Status = status, RespondedAt = nowUtc });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddPrepAsync(EventPrep prep, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.EventPreps.Add(prep);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddSuggestionAsync(EventTimeSuggestion suggestion, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.EventTimeSuggestions.Add(suggestion);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResolveSuggestionAsync(Guid suggestionId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.EventTimeSuggestions.Where(s => s.Id == suggestionId).ExecuteUpdateAsync(u => u.SetProperty(s => s.Resolved, true), ct);
    }

    public async Task VoteAsync(Guid suggestionId, string memberName, string status, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var existing = await db.EventTimeVotes.FirstOrDefaultAsync(v => v.SuggestionId == suggestionId && v.MemberName == memberName, ct);
        if (existing is not null) existing.Status = status;
        else db.EventTimeVotes.Add(new EventTimeVote { SuggestionId = suggestionId, MemberName = memberName, Status = status });
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class ChatStore(IDbContextFactory<ClubDb> dbf) : IChatStore
{
    public async Task<List<ClubChatMessage>> LatestAsync(Guid groupId, int max, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var newest = await db.Chat.AsNoTracking().Where(m => m.GroupId == groupId).OrderByDescending(m => m.SentAt).Take(max).ToListAsync(ct);
        newest.Reverse();
        return newest;
    }

    public async Task AddAsync(ClubChatMessage message, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Chat.Add(message);
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class BulletinStore(IDbContextFactory<ClubDb> dbf) : IBulletinStore
{
    public async Task<List<BulletinPost>> LatestAsync(Guid groupId, int max, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Posts.AsNoTracking().Include(p => p.Member).Where(p => p.GroupId == groupId)
            .OrderByDescending(p => p.PostedAt).Take(max).ToListAsync(ct);
    }

    public async Task AddAsync(BulletinPost post, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.SaveRowsAsync([post], EntityState.Added, ct);
    }

    public async Task<bool> DeleteOwnAsync(Guid postId, Guid memberId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Posts.Where(p => p.Id == postId && p.MemberId == memberId).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task ClearAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Posts.Where(p => p.GroupId == groupId).ExecuteDeleteAsync(ct);
    }
}
