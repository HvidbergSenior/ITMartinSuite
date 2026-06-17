using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface IPlannedTransactionService
{
    Task<List<PlannedTransaction>> GetAllAsync();

    Task<PlannedTransaction> AddAsync(
        PlannedTransaction transaction,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id);
}
