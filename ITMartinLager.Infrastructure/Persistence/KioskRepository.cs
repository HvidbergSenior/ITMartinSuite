using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

// Kiosk prices (/kiosk). A context per call, like the other repositories.
public sealed class KioskRepository(IDbContextFactory<LagerDb> dbf) : IKioskRepository
{
    public async Task<List<KioskPrice>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.KioskPrices.AsNoTracking().OrderBy(p => p.Series).ToListAsync(ct);
    }

    public async Task SaveAsync(KioskPrice price, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (price.Id == 0) db.KioskPrices.Add(price); else db.KioskPrices.Update(price);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.KioskPrices.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }
}
