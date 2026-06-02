using ITMartinBudget.Application.Extensions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class FamilyPlanningService
    : IFamilyPlanningService
{
    private readonly IDashboardService _dashboardService;
    public FamilyPlanningService(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<FamilyPlanningViewModel>
    BuildAsync()
{

    var dashboard =
        await _dashboardService
            .BuildDashboardAsync();

    return new FamilyPlanningViewModel
    {
        CurrentIncome =
            dashboard.TotalIncome,

        CurrentExpenses =
            dashboard.TotalExpenses,

        CurrentNetAmount =
            dashboard.NetAmount,
        MonthsLoaded = dashboard.MonthsLoaded,

        Transactions = dashboard.Transactions,
        BudgetGroups =
            dashboard.BudgetGroupSummaries
                .OrderByDescending(x => x.DisplayAmount)
                .ToList(),
        FixedIncome =
        dashboard.FixedIncome
    };
}
}