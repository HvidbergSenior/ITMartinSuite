using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Infrastructure;

// One short DbContext per call: Blazor Server keeps scoped services for the whole visit, so a shared context would
// keep showing the site as it was when the page opened.

internal sealed class SettingsRepository(IDbContextFactory<HjemDb> dbf) : ISettingsRepository
{
    public async Task<Settings> GetAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Settings.AsNoTracking().FirstAsync(ct);
    }

    public async Task SaveAsync(Settings settings, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Settings.Update(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task EnsureAsync(Settings first, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (await db.Settings.AnyAsync(ct)) return;
        db.Settings.Add(first);
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class DayNoteRepository(IDbContextFactory<HjemDb> dbf) : IDayNoteRepository
{
    public async Task<DayNote?> GetAsync(string day, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.DayNotes.AsNoTracking().FirstOrDefaultAsync(d => d.Day == day, ct);
    }

    public async Task<List<DayNote>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.DayNotes.AsNoTracking().ToListAsync(ct);
    }

    public async Task<DayNote> SaveAsync(string day, string plan, string done, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var row = await db.DayNotes.FirstOrDefaultAsync(d => d.Day == day, ct);
        if (row is null) db.DayNotes.Add(row = new DayNote { Day = day });
        row.Plan = plan;
        row.Done = done;
        await db.SaveChangesAsync(ct);
        return row;
    }
}

internal sealed class PageRepository(IDbContextFactory<HjemDb> dbf) : IPageRepository
{
    public async Task<Page?> BySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
    }

    public async Task<List<Page>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync(ct);
    }

    public async Task<List<Page>> InMenuAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Pages.AsNoTracking().Where(p => p.InMenu).ToListAsync(ct);
    }

    public async Task<List<Page>> ChildrenAsync(string slug, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var prefix = slug + "/";
        return await db.Pages.AsNoTracking().Where(p => p.Slug.StartsWith(prefix)).OrderBy(p => p.Sort).ThenBy(p => p.Title).ToListAsync(ct);
    }

    public async Task<bool> SlugTakenAsync(string slug, int exceptId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Pages.AnyAsync(p => p.Slug == slug && p.Id != exceptId, ct);
    }

    public async Task<int> SaveAsync(Page edit, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var page = edit.Id == 0 ? db.Pages.Add(new Page()).Entity : await db.Pages.FirstAsync(p => p.Id == edit.Id, ct);
        page.Slug = edit.Slug; page.Title = edit.Title; page.Body = edit.Body; page.InMenu = edit.InMenu; page.Sort = edit.Sort;
        page.UpdatedAt = edit.UpdatedAt;
        await db.SaveChangesAsync(ct);
        return page.Id;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Pages.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> AnyUsesAsync(string text, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Pages.AnyAsync(p => p.Body.Contains(text), ct);
    }
}

internal sealed class AppLinkRepository(IDbContextFactory<HjemDb> dbf) : IAppLinkRepository
{
    public async Task<List<AppLink>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.AppLinks.AsNoTracking().OrderBy(a => a.Sort).ToListAsync(ct);
    }

    public async Task<List<AppLink>> ShownAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.AppLinks.AsNoTracking().Where(a => a.Show).OrderBy(a => a.Sort).ToListAsync(ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.AppLinks.AnyAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<AppLink> links, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        foreach (var a in links)
            if (a.Id == 0) db.AppLinks.Add(a); else db.AppLinks.Update(a);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> SetAwakeAsync(int id, bool awake, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.AppLinks.Where(a => a.Id == id).ExecuteUpdateAsync(x => x.SetProperty(a => a.Awake, awake), ct) == 1;
    }

    public async Task<bool> SetShowAsync(int id, bool show, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.AppLinks.Where(a => a.Id == id).ExecuteUpdateAsync(x => x.SetProperty(a => a.Show, show), ct) == 1;
    }
}

internal sealed class PostRepository(IDbContextFactory<HjemDb> dbf) : IPostRepository
{
    public async Task<List<Post>> TimelineAsync(bool family, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Posts.Include(p => p.Media.OrderBy(m => m.Sort)).AsNoTracking()
            .Where(p => family || p.Visibility == Visibility.Offentlig)
            .OrderByDescending(p => p.At).ThenByDescending(p => p.Id).ToListAsync(ct);
    }

    public async Task<int> CountAsync(Visibility visibility, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Posts.CountAsync(p => p.Visibility == visibility, ct);
    }

    public async Task<Post?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Posts.Include(p => p.Media.OrderBy(m => m.Sort)).AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task SaveAsync(Post edit, IReadOnlyCollection<int> removedMediaIds, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (edit.Id == 0)
        {
            db.Posts.Add(edit);
        }
        else
        {
            var p = await db.Posts.Include(x => x.Media).FirstAsync(x => x.Id == edit.Id, ct);
            p.Title = edit.Title; p.Body = edit.Body; p.At = edit.At; p.Visibility = edit.Visibility;
            foreach (var gone in p.Media.Where(m => removedMediaIds.Contains(m.Id)).ToList()) { p.Media.Remove(gone); db.Media.Remove(gone); }
            foreach (var added in edit.Media.Where(m => m.Id == 0)) p.Media.Add(added);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<Post?> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var p = await db.Posts.Include(x => x.Media).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        db.Posts.Remove(p);
        await db.SaveChangesAsync(ct);
        return p;
    }

    public async Task<Media?> MediaAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Media.Include(m => m.Post).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
    }
}

internal sealed class ChatRepository(IDbContextFactory<HjemDb> dbf) : IChatRepository
{
    public async Task<ChatThread?> ByVisitorAsync(string visitorKey, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Threads.Include(t => t.Messages.OrderBy(m => m.At)).AsNoTracking().FirstOrDefaultAsync(t => t.VisitorKey == visitorKey, ct);
    }

    public async Task<ChatThread?> ByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Threads.Include(t => t.Messages.OrderBy(m => m.At)).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<List<ChatThread>> NewestAsync(int take, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Threads.Include(t => t.Messages).AsNoTracking().OrderByDescending(t => t.LastAt).Take(take).ToListAsync(ct);
    }

    public async Task<List<int>> ActiveSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Threads.Where(t => t.LastAt > sinceUtc).Select(t => t.Id).ToListAsync(ct);
    }

    public async Task<ChatThread?> ChangeAsync(int id, Func<ChatThread, Task<bool>> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var t = await db.Threads.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;
        if (await change(t)) await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<ChatThread> ChangeForVisitorAsync(string visitorKey, Action<ChatThread> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var t = await db.Threads.FirstOrDefaultAsync(x => x.VisitorKey == visitorKey, ct);
        if (t is null) { t = new ChatThread { VisitorKey = visitorKey }; db.Threads.Add(t); }
        change(t);
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<bool> SetVisitorEmailAsync(string visitorKey, string email, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Threads.Where(t => t.VisitorKey == visitorKey).ExecuteUpdateAsync(s => s.SetProperty(t => t.VisitorEmail, email), ct) > 0;
    }

    public async Task<bool> SetVisitorPushAsync(string visitorKey, string push, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Threads.Where(t => t.VisitorKey == visitorKey).ExecuteUpdateAsync(s => s.SetProperty(t => t.VisitorPush, push), ct) > 0;
    }

    public async Task MarkReadAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Threads.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.UnreadForOwner, false), ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Messages.Where(m => m.ThreadId == id).ExecuteDeleteAsync(ct);
        await db.Threads.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
    }
}

internal sealed class PushSubscriptionRepository(IDbContextFactory<HjemDb> dbf) : IPushSubscriptionRepository
{
    public async Task<List<PushSub>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.PushSubs.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddIfNewAsync(PushSub sub, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (await db.PushSubs.AnyAsync(s => s.Endpoint == sub.Endpoint, ct)) return;
        db.PushSubs.Add(sub);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var list = ids.ToList();
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.PushSubs.Where(s => list.Contains(s.Id)).ExecuteDeleteAsync(ct);
    }
}

internal sealed class HitRepository(IDbContextFactory<HjemDb> dbf) : IHitRepository
{
    public async Task<List<string>> OwnerHashesAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.OwnerIps.Select(o => o.Hash).ToListAsync(ct);
    }

    public async Task AddOwnerHashAsync(string hash, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (await db.OwnerIps.AnyAsync(o => o.Hash == hash, ct)) return;
        db.OwnerIps.Add(new OwnerIp { Hash = hash });
        await db.SaveChangesAsync(ct);
    }

    public async Task PurgeOlderThanAsync(DateTime utc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Hits.Where(h => h.At < utc).ExecuteDeleteAsync(ct);
    }

    public async Task<long> AddAsync(Hit hit, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Hits.Add(hit);
        await db.SaveChangesAsync(ct);
        return hit.Id;
    }

    public async Task SetSecondsAsync(long id, int seconds, DateTime notBeforeUtc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Hits.Where(h => h.Id == id && h.At > notBeforeUtc).ExecuteUpdateAsync(x => x.SetProperty(h => h.Seconds, seconds), ct);
    }

    public async Task<int> DeleteByVisitorAsync(string visitor, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Hits.Where(h => h.Visitor == visitor).ExecuteDeleteAsync(ct);
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Hits.ExecuteDeleteAsync(ct);
    }

    public async Task<List<Hit>> SinceAsync(DateTime utc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Hits.AsNoTracking().Where(h => h.At >= utc).ToListAsync(ct);
    }

    public async Task<Dictionary<string, DateTime>> LastSeenAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return (await db.Hits.GroupBy(h => h.Host).Select(g => new { g.Key, Last = g.Max(h => h.At) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Last);
    }
}
