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

    public async Task<ReceiptTransaction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Transactions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task SetTemplateAsync(
        Guid id,
        bool isTemplate,
        CancellationToken cancellationToken = default)
    {
        var tx = await _db.Transactions.FindAsync([id], cancellationToken);
        if (tx is null) return;
        tx.IsTemplate = isTemplate;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tx = await _db.Transactions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tx is null) return;
        _db.Transactions.Remove(tx);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ReceiptTransaction>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Transactions
            .Include(x => x.Items)
            .Where(x => x.IsTemplate)
            .OrderBy(x => x.MerchantName)
            .ToListAsync(cancellationToken);
    }
}
