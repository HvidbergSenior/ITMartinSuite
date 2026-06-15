using ITMartin.Receipt.Application.Interfaces;
using ITMartin.Receipt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Receipt.Infrastructure.Repositories;

public sealed class ReceiptRepository : IReceiptRepository
{
    private readonly ReceiptDbContext _db;

    public ReceiptRepository(ReceiptDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(
        ReceiptTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ReceiptTransaction>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Transactions
            .Include(x => x.Items)
            .OrderByDescending(x => x.ScannedAt)
            .ToListAsync(cancellationToken);
    }
}
