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
