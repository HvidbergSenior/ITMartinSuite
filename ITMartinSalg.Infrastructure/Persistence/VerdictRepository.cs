using ITMartinSalg.Application;
using ITMartinSalg.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinSalg.Infrastructure.Persistence;

// "Hvad syntes du?" (2026-11-21). A context per call, like the other repositories.
public sealed class VerdictRepository(IDbContextFactory<SalgDb> dbf) : IVerdictRepository
{
    public async Task AddAsync(ItemVerdict v, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Verdicts.Add(v);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ItemVerdict?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Verdicts.FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task SaveAsync(ItemVerdict v, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Verdicts.Update(v);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ItemVerdict>> ApprovedForAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Verdicts.AsNoTracking().Where(v => v.Status == ItemVerdict.Approved && itemIds.Contains(v.ItemId))
            .OrderByDescending(v => v.DecidedAt).ToListAsync(ct);
    }

    public async Task<List<ItemVerdict>> NewAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Verdicts.AsNoTracking().Where(v => v.Status == ItemVerdict.New).OrderBy(v => v.CreatedAt).Take(200).ToListAsync(ct);
    }

    public async Task<int> NewOfAsync(string customer, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Verdicts.CountAsync(v => v.Customer == customer && v.Status == ItemVerdict.New, ct);
    }
}
