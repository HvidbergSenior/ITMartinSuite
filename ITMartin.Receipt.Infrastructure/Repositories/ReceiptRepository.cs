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
        // Auto-learning: only one reference example is kept per merchant. When this
        // scan is good enough to become the new one, retire whichever one held that
        // role before it - no user action involved on either end.
        if (transaction.IsTemplate && !string.IsNullOrWhiteSpace(transaction.MerchantName))
        {
            var previousReferences = await _db.Transactions
                .Where(x => x.IsTemplate && x.MerchantName.ToLower() == transaction.MerchantName.ToLower())
                .ToListAsync(cancellationToken);

            foreach (var previous in previousReferences)
                previous.IsTemplate = false;
        }

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

    public async Task<ReceiptTransaction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Transactions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tx = await _db.Transactions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tx is null) return;

        if (tx.ImageFileName is not null)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "data", "receipts", tx.ImageFileName);
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }

        _db.Transactions.Remove(tx);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReceiptTransaction?> GetReferenceAsync(
        string merchantName,
        CancellationToken cancellationToken = default)
    {
        return await _db.Transactions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.IsTemplate && x.MerchantName.ToLower() == merchantName.ToLower(), cancellationToken);
    }
}
