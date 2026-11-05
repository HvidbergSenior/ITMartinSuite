using Microsoft.EntityFrameworkCore;

namespace ITMartinSalg.Infrastructure.Persistence;

// Byd + Mit bundt (2026-11-05). A context per call, like the other repositories.
public sealed class OfferRepository(IDbContextFactory<SalgDb> dbf) : IOfferRepository
{
    public async Task<Bid?> BidAsync(string customer, int itemId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Bids.FirstOrDefaultAsync(b => b.Customer == customer && b.ItemId == itemId, ct);
    }

    public async Task SaveBidAsync(Bid bid, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (bid.Id == 0) db.Bids.Add(bid); else db.Bids.Update(bid);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { }   // pressed twice at once: the unique index keeps one bid
    }

    public async Task<List<Bid>> MyBidsAsync(string customer, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Bids.AsNoTracking().Where(b => b.Customer == customer).ToListAsync(ct);
    }

    public async Task<List<Bid>> AllBidsAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Bids.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddBundleAsync(BundleOffer bundle, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Bundles.Add(bundle);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<BundleOffer>> BundlesAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Bundles.AsNoTracking().OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync(ct);
    }

    public async Task<BundleOffer?> BundleAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Bundles.FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task SaveBundleAsync(BundleOffer bundle, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Bundles.Update(bundle);
        await db.SaveChangesAsync(ct);
    }
}
