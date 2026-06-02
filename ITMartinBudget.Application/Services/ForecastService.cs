using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class ForecastService
    : IForecastService
{
    private readonly IDashboardService
        _dashboardService;

    public ForecastService(
        IDashboardService dashboardService)
    {
        _dashboardService =
            dashboardService;
    }

    public async Task<ForecastViewModel>
        BuildAsync()
    {
        var dashboard =
            await _dashboardService
                .BuildDashboardAsync();

        var latestSalary =
            dashboard.Transactions
                .Where(x =>
                    x.BudgetGroup ==
                    BudgetGroup.FixedIncome)
                .OrderByDescending(x =>
                    x.Date)
                .FirstOrDefault();

        var monthlySalary =
            Math.Abs(
                latestSalary?.Amount ?? 0);

        var recurringExpenses =
            dashboard.Transactions
                .Where(x =>
                    x.Amount < 0)
                .Where(x =>
                    x.BudgetGroup ==
                    BudgetGroup.FixedExpense
                    ||
                    x.BudgetGroup ==
                    BudgetGroup.Subscriptions)
                .GroupBy(x =>
                    x.Description);
                
       
    
        return new ForecastViewModel
        {
            CurrentBalance =
                dashboard.NetAmount,

            MonthlySalary =
                monthlySalary,

           // RecurringExpenses =
             //   recurringExpenses,

           
        };
    }
}