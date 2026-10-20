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

    public async Task<int> CountAsync(string boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.CountAsync(p => p.BoxCode == boxCode, ct);
    }

    public async Task<Dictionary<string, int>> PerBoxAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.GroupBy(p => p.BoxCode).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }
}

public sealed class ScanRepository(IDbContextFactory<VareinfoDb> dbf) : IScanRepository
{
    public async Task<int> AddOrCountAsync(string code, string boxCode, Taker who, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var same = await db.Scans.FirstOrDefaultAsync(s => s.Code == code && s.BoxCode == boxCode && s.SentAt == null, ct);
        if (same is null) db.Scans.Add(same = new Scan { Code = code, BoxCode = boxCode, HelperId = who.Id, Helper = who.Name });
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

    public async Task<int> CountAsync(string boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.Where(s => s.BoxCode == boxCode).SumAsync(s => s.Quantity, ct);
    }

    public async Task<Dictionary<string, int>> PerBoxAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.GroupBy(s => s.BoxCode).Select(g => new { g.Key, Count = g.Sum(s => s.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
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

public sealed class BoxClaimRepository(IDbContextFactory<VareinfoDb> dbf) : IBoxClaimRepository
{
    public async Task<BoxClaim?> GetAsync(string boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BoxClaims.AsNoTracking().FirstOrDefaultAsync(c => c.BoxCode == boxCode, ct);
    }

    public async Task SaveAsync(BoxClaim claim, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (await db.BoxClaims.AnyAsync(c => c.BoxCode == claim.BoxCode, ct)) db.BoxClaims.Update(claim);
        else db.BoxClaims.Add(claim);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<BoxClaim>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BoxClaims.AsNoTracking().ToListAsync(ct);
    }

    public async Task<List<BoxClaim>> OpenForAsync(int helperId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BoxClaims.AsNoTracking().Where(c => c.HelperId == helperId && c.DoneAt == null)
            .OrderByDescending(c => c.LastAt).ToListAsync(ct);
    }
}
