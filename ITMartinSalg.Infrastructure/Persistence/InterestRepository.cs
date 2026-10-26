using Microsoft.EntityFrameworkCore;

namespace ITMartinSalg.Infrastructure.Persistence;

// The "interessant" marks. A context per call, like the other repositories.
public sealed class InterestRepository(IDbContextFactory<SalgDb> dbf) : IInterestRepository
{
    public async Task<Interest?> FindAsync(string customer, int itemId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Interests.FirstOrDefaultAsync(i => i.Customer == customer && i.ItemId == itemId, ct);
    }

    public async Task AddAsync(Interest interest, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Interests.Add(interest);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { }   // pressed twice at once: the unique index keeps one mark
    }

    public async Task RemoveAsync(Interest interest, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Interests.Where(i => i.Id == interest.Id).ExecuteDeleteAsync(ct);
    }

    public async Task<Dictionary<int, int>> CountsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Interests.Where(i => itemIds.Contains(i.ItemId)).GroupBy(i => i.ItemId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<List<int>> MineAsync(string customer, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Interests.Where(i => i.Customer == customer).OrderByDescending(i => i.MarkedAt).Select(i => i.ItemId).ToListAsync(ct);
    }

    public async Task<List<(int ItemId, int Count, DateTime LastAt)>> TopAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var rows = await db.Interests.GroupBy(i => i.ItemId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(i => i.MarkedAt) })
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.LastAt).Take(max).ToListAsync(ct);
        return rows.Select(r => (r.Key, r.Count, r.LastAt)).ToList();
    }
}
