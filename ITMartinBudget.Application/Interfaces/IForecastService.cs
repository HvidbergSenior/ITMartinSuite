using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IForecastService
{
    Task<ForecastViewModel>
        BuildAsync();
}