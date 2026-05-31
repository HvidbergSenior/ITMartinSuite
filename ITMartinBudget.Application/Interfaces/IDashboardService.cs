using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel>
        BuildDashboardAsync();
}