using ITMartinMarket.Application.Interfaces;
using ITMartinMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMarket.Infrastructure.Repositories;

public sealed class SaleItemRepository(MarketDbContext db) : ISaleItemRepository
{
    public Task<List<SaleItem>> GetActiveAsync(CancellationToken ct = default)
        => db.Items
            .Where(i => !i.IsSold)
            .Include(i => i.Bids)
            .Include(i => i.Messages)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public Task<List<SaleItem>> GetAllAsync(CancellationToken ct = default)
        => db.Items
            .Include(i => i.Bids)
            .Include(i => i.Messages)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public Task<SaleItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Items
            .Include(i => i.Bids)
            .Include(i => i.Messages)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task AddAsync(SaleItem item, CancellationToken ct = default)
    {
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SaleItem item, CancellationToken ct = default)
    {
        db.Items.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddBidAsync(Bid bid, CancellationToken ct = default)
    {
        db.Bids.Add(bid);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddMessageAsync(ItemMessage message, CancellationToken ct = default)
    {
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
    }
}
