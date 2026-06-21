using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IFinancialAdvisorService
{
    Task<string> GetAdviceAsync(
        ForecastViewModel forecast,
        ForwardBudgetViewModel budget,
        CancellationToken cancellationToken = default);
}
