using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Persistence;

public sealed class PriceAlertRepository(MagicDbContext db) : IPriceAlertRepository
{
    public Task<List<PriceAlert>> GetActiveAsync(CancellationToken ct = default)
        => db.PriceAlerts
            .Where(a => !a.Dismissed)
            .OrderByDescending(a => a.DetectedAt)
            .ToListAsync(ct);

    public async Task AddAsync(PriceAlert alert, CancellationToken ct = default)
    {
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync(ct);
    }

    public async Task DismissAsync(Guid id, CancellationToken ct = default)
    {
        var alert = await db.PriceAlerts.FindAsync([id], ct);
        if (alert is not null)
        {
            alert.Dismissed = true;
            await db.SaveChangesAsync(ct);
        }
    }
}
