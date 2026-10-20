using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

// Orders with their lines (OrderLines). A context per call, like the other repositories.
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

    public async Task<Order?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> ByTokenAsync(string token, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Token == token, ct);
    }

    public async Task<List<Order>> PendingAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.Include(o => o.Lines).Where(o => o.Status == OrderStatuses.Pending).OrderBy(o => o.Id).ToListAsync(ct);
    }

    public async Task<Dictionary<int, int>> ReservedAsync(IReadOnlyCollection<int> itemIds, DateTime utcNow, CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.OrderLines
            .Where(l => itemIds.Contains(l.ItemId) && db.Orders.Any(o => o.Id == l.OrderId && o.Status == OrderStatuses.Pending && o.ExpiresAt > utcNow))
            .GroupBy(l => l.ItemId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<List<Order>> RecentAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Orders.Include(o => o.Lines).OrderByDescending(o => o.Id).Take(max).ToListAsync(ct);
    }
}

public sealed class CampaignRepository(IDbContextFactory<LagerDb> dbf) : ICampaignRepository
{
    public async Task<List<Campaign>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Campaigns.OrderBy(c => c.Weekday == null).ThenBy(c => c.Weekday).ThenBy(c => c.DayOfMonth).ThenBy(c => c.Id).ToListAsync(ct);
    }

    public async Task SaveAsync(Campaign campaign, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (campaign.Id == 0) db.Campaigns.Add(campaign);
        else db.Campaigns.Update(campaign);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Campaigns.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
    }
}
