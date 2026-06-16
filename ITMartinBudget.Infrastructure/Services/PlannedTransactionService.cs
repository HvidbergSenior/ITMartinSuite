using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class PlannedTransactionService
    : IPlannedTransactionService
{
    private readonly BudgetDbContext _db;

    public PlannedTransactionService(BudgetDbContext db)
    {
        _db = db;
    }

    public Task<List<PlannedTransaction>> GetAllAsync()
        => _db.PlannedTransactions
            .OrderBy(x => x.ExpectedDate)
            .ToListAsync();

    public async Task<PlannedTransaction> AddAsync(
        PlannedTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        _db.PlannedTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.PlannedTransactions
            .FindAsync(id);

        if (entity is not null)
        {
            _db.PlannedTransactions.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
