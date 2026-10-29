using Microsoft.EntityFrameworkCore;

namespace ITMartinVareinfo.Infrastructure.Persistence;

// A context per call (IDbContextFactory) - Blazor Server circuits live long, a shared context would go stale, and the
// uploads of many phones run side by side.
public sealed class PhotoRepository(IDbContextFactory<VareinfoDb> dbf) : IPhotoRepository
{
    public async Task<Photo> AddAsync(Photo photo, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Photos.Add(photo);
        await db.SaveChangesAsync(ct);
        return photo;
    }

    public async Task<Photo?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.FindAsync([id], ct);
    }

    public async Task UpdateAsync(Photo photo, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Photos.Update(photo);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Photo>> UnprocessedAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.Where(p => !p.Ready).OrderBy(p => p.Id).Take(max).ToListAsync(ct);
    }

    public async Task<List<Photo>> UnsentAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.Where(p => p.Ready && p.SentAt == null).OrderBy(p => p.Id).Take(max).ToListAsync(ct);
    }

    public async Task MarkSentAsync(IReadOnlyCollection<int> ids, DateTime now, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Photos.Where(p => ids.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.SentAt, now), ct);
    }

    public async Task<int> CountAsync(int sessionId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.CountAsync(p => p.SessionId == sessionId, ct);
    }

    public async Task<List<HelperTally>> PerSessionAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.GroupBy(p => new { p.SessionId, p.Helper })
            .Select(g => new HelperTally(g.Key.SessionId, g.Key.Helper, g.Count(), g.Max(p => p.TakenAt))).ToListAsync(ct);
    }
}

public sealed class ScanRepository(IDbContextFactory<VareinfoDb> dbf) : IScanRepository
{
    public async Task<int> AddOrCountAsync(string code, WorkSession session, Taker who, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var same = await db.Scans.FirstOrDefaultAsync(s => s.Code == code && s.SessionId == session.Id && s.SentAt == null, ct);
        if (same is null) db.Scans.Add(same = new Scan { Code = code, SessionId = session.Id, BoxCode = session.BoxCode, HelperId = who.Id, Helper = who.Name });
        else same.Quantity++;
        await db.SaveChangesAsync(ct);
        return same.Quantity;
    }

    public async Task<List<Scan>> UnsentAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.Where(s => s.SentAt == null).OrderBy(s => s.Id).Take(max).ToListAsync(ct);
    }

    public async Task MarkSentAsync(IReadOnlyCollection<int> ids, DateTime now, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Scans.Where(s => ids.Contains(s.Id)).ExecuteUpdateAsync(x => x.SetProperty(s => s.SentAt, now), ct);
    }

    public async Task<int> CountAsync(int sessionId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.Where(s => s.SessionId == sessionId).SumAsync(s => s.Quantity, ct);
    }

    public async Task<List<HelperTally>> PerSessionAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.GroupBy(s => new { s.SessionId, s.Helper })
            .Select(g => new HelperTally(g.Key.SessionId, g.Key.Helper, g.Sum(s => s.Quantity), g.Max(s => s.TakenAt))).ToListAsync(ct);
    }
}

public sealed class HelperRepository(IDbContextFactory<VareinfoDb> dbf) : IHelperRepository
{
    public async Task<List<Helper>> ActiveAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Helpers.Where(h => h.Active).OrderBy(h => h.Name).ToListAsync(ct);
    }

    public async Task<Helper?> ByCodeAsync(string code, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Helpers.FirstOrDefaultAsync(h => h.Code == code, ct);
    }

    public async Task<Helper?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Helpers.FindAsync([id], ct);
    }

    public async Task AddAsync(Helper helper, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Helpers.Add(helper);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Helper helper, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Helpers.Update(helper);
        await db.SaveChangesAsync(ct);
    }
}

// The scanning sessions (2026-10-29: numbered, named after place and/or purpose).
public sealed class SessionRepository(IDbContextFactory<VareinfoDb> dbf) : ISessionRepository
{
    public async Task<List<WorkSession>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Sessions.AsNoTracking().OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).ToListAsync(ct);
    }

    public async Task<WorkSession?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task SaveAsync(WorkSession session, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (session.Id == 0) db.Sessions.Add(session);
        else db.Sessions.Update(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int sessionId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Sessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(ct);
    }
}
