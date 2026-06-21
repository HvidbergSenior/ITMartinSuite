using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IFinancialForecastService
{
    Task<ForecastViewModel> BuildAsync(int projectedMonths = 3);
}
