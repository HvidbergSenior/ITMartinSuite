using Microsoft.EntityFrameworkCore;

namespace ITMartinVareinfo.Infrastructure.Persistence;

// A context per call (IDbContextFactory) - Blazor Server circuits live long, a shared context would go stale.
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

    public async Task<List<Photo>> WantedAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.Where(p => p.Waiting && p.ReadWanted).OrderBy(p => p.Id).Take(max).ToListAsync(ct);
    }

    public async Task<int> WantReadAsync(string? boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Photos.Where(p => p.Waiting && !p.ReadWanted && (boxCode == null || p.BoxCode == boxCode))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReadWanted, true), ct);
    }

    public async Task<List<BoxPhotos>> BoxesAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var rows = await db.Photos.Where(p => p.BoxCode != "")
            .GroupBy(p => p.BoxCode)
            .Select(g => new BoxPhotos(g.Key, g.Count(p => p.Waiting), g.Count(p => p.Waiting && p.ReadWanted),
                g.Count(p => !p.Waiting), g.Sum(p => p.Found), 0, 0))
            .ToListAsync(ct);
        return rows.OrderByDescending(b => b.BoxCode).ToList();
    }
}

public sealed class KolofonShotRepository(IDbContextFactory<VareinfoDb> dbf) : IKolofonShotRepository
{
    public async Task AddAsync(KolofonShot shot, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.KolofonShots.Add(shot);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<KolofonShot>> WaitingAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.KolofonShots.Where(s => s.ReadAt == null).OrderBy(s => s.TakenAt).Take(max).ToListAsync(ct);
    }

    public async Task<int> WaitingCountAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.KolofonShots.CountAsync(s => s.ReadAt == null, ct);
    }

    public async Task<List<KolofonShot>> RecentlyReadAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.KolofonShots.Where(s => s.ReadAt != null).OrderByDescending(s => s.ReadAt).Take(max).ToListAsync(ct);
    }

    public async Task<KolofonShot?> LatestForAsync(int itemId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.KolofonShots.Where(s => s.ItemId == itemId).OrderByDescending(s => s.TakenAt).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(IEnumerable<KolofonShot> shots, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.KolofonShots.UpdateRange(shots);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ScanRepository(IDbContextFactory<VareinfoDb> dbf) : IScanRepository
{
    public async Task<int> AddOrCountAsync(string code, int boxId, string boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var same = await db.Scans.FirstOrDefaultAsync(s => s.Code == code && s.BoxCode == boxCode && s.Waiting, ct);
        if (same is null) db.Scans.Add(same = new Scan { Code = code, BoxId = boxId, BoxCode = boxCode });
        else same.Quantity++;
        await db.SaveChangesAsync(ct);
        return same.Quantity;
    }

    public async Task<List<Scan>> WantedAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.Where(s => s.Waiting && s.ReadWanted).OrderBy(s => s.Id).Take(max).ToListAsync(ct);
    }

    public async Task<int> WantReadAsync(string? boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Scans.Where(s => s.Waiting && !s.ReadWanted && (boxCode == null || s.BoxCode == boxCode))
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.ReadWanted, true), ct);
    }

    public async Task UpdateAsync(IEnumerable<Scan> scans, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Scans.UpdateRange(scans);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, (int Waiting, int Wanted)>> PerBoxAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var rows = await db.Scans.Where(s => s.Waiting).GroupBy(s => s.BoxCode)
            .Select(g => new { g.Key, Waiting = g.Sum(s => s.Quantity), Wanted = g.Where(s => s.ReadWanted).Sum(s => s.Quantity) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => (r.Waiting, r.Wanted));
    }
}
