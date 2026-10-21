using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

// Salg's sales booked here (SaleBookings, Ref unique). A context per call, like the other repositories.
public sealed class SaleBookingRepository(IDbContextFactory<LagerDb> dbf) : ISaleBookingRepository
{
    public async Task<bool> ExistsAsync(string saleRef, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.SaleBookings.AnyAsync(b => b.Ref == saleRef, ct);
    }

    public async Task BookAsync(string saleRef, IEnumerable<Item> soldItems, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Items.UpdateRange(soldItems);
        db.SaleBookings.Add(new SaleBooking { Ref = saleRef });
        await db.SaveChangesAsync(ct);   // one transaction: the copies and the booking, or neither
    }
}
