using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

public sealed class OrderRepository(IDbContextFactory<LagerDb> dbf) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Orders.Update(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Order?> ByTokenAsync(string token, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.FirstOrDefaultAsync(o => o.Token == token, ct);
    }

    public async Task<List<Order>> PendingAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.Where(o => o.Status == OrderStatuses.Pending).OrderBy(o => o.Id).ToListAsync(ct);
    }

    public async Task<Dictionary<int, int>> ReservedAsync(IReadOnlyCollection<int> itemIds, DateTime utcNow, CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.Where(o => o.Status == OrderStatuses.Pending && o.ExpiresAt > utcNow && itemIds.Contains(o.ItemId))
            .GroupBy(o => o.ItemId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<List<Order>> RecentAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.OrderByDescending(o => o.Id).Take(max).ToListAsync(ct);
    }

    public async Task SetHandledAsync(int id, bool handled, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Handled, handled), ct);
    }
}
