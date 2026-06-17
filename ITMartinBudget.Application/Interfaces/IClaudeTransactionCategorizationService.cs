using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IClaudeTransactionCategorizationService
{
    Task<ClaudeCategorizationResult> CategorizeAsync(
        string description,
        decimal amount,
        CancellationToken cancellationToken = default);
}
