using ITMartinBudget.Application.Extensions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public sealed class FinancialForecastService : IFinancialForecastService
{
    private readonly IDashboardService _dashboard;
    private readonly IForwardBudgetService _forward;

    public FinancialForecastService(IDashboardService dashboard, IForwardBudgetService forward)
    {
        _dashboard = dashboard;
        _forward = forward;
    }

    public async Task<ForecastViewModel> BuildAsync(int projectedMonths = 3)
    {
        var dashTask = _dashboard.BuildDashboardAsync();
        var forwardTask = _forward.BuildAsync();
        await Task.WhenAll(dashTask, forwardTask);

        var transactions = dashTask.Result.Transactions;
        var forwardModel = forwardTask.Result;

        var history = transactions
            .Where(t => t.BudgetGroup != BudgetGroup.OverførslerTilFraOpsparingsKonto)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new MonthlySnapshot
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                Expenses = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount))
            })
            .OrderBy(s => s.Year).ThenBy(s => s.Month)
            .ToList();

        if (history.Count < 2)
        {
            return new ForecastViewModel
            {
                History = history,
                AvgIncome = history.Any() ? history.Average(h => h.Income) : 0,
                AvgExpenses = history.Any() ? history.Average(h => h.Expenses) : 0,
            };
        }

        var incomeSlope = CalculateSlope(history.Select(h => h.Income).ToList());
        var expenseSlope = CalculateSlope(history.Select(h => h.Expenses).ToList());
        var avgIncome = history.Average(h => h.Income);
        var avgExpenses = history.Average(h => h.Expenses);

        // Project from last actual value + trend
        var lastIncome = history.Last().Income;
        var lastExpenses = history.Last().Expenses;
        var lastDate = new DateTime(history.Last().Year, history.Last().Month, 1);

        var projected = Enumerable.Range(1, projectedMonths).Select(i =>
        {
            var d = lastDate.AddMonths(i);
            return new MonthlySnapshot
            {
                Year = d.Year,
                Month = d.Month,
                Income = Math.Max(0, lastIncome + incomeSlope * i),
                Expenses = Math.Max(0, lastExpenses + expenseSlope * i),
                IsProjected = true
            };
        }).ToList();

        var cuttable = BuildCuttableExpenses(forwardModel);

        return new ForecastViewModel
        {
            History = history,
            Projected = projected,
            AvgIncome = avgIncome,
            AvgExpenses = avgExpenses,
            IncomeSlope = incomeSlope,
            ExpenseSlope = expenseSlope,
            CuttableExpenses = cuttable
        };
    }

    private static List<CuttableExpenseItem> BuildCuttableExpenses(ForwardBudgetViewModel forward)
    {
        var items = new List<CuttableExpenseItem>();

        // Subscriptions as one block
        var subTotal = forward.RecurringAdjustableExpenses.Sum(e => e.MonthlyAmount);
        if (subTotal > 0)
        {
            items.Add(new CuttableExpenseItem
            {
                Group = BudgetGroup.Subscriptions,
                DisplayName = "Abonnementer",
                MonthlyAverage = subTotal,
                TransactionCount = forward.RecurringAdjustableExpenses.Count,
                GroupType = BudgetGroupType.RecurringAdjustable
            });
        }

        foreach (var g in forward.AdjustableGroups.Where(g => g.MonthlyAverage >= 100))
        {
            items.Add(new CuttableExpenseItem
            {
                Group = g.BudgetGroup,
                DisplayName = g.BudgetGroup.ToDisplayName(),
                MonthlyAverage = g.MonthlyAverage,
                TransactionCount = g.TransactionCount,
                GroupType = BudgetGroupType.Adjustable
            });
        }

        foreach (var g in forward.SemiAdjustableGroups.Where(g => g.MonthlyAverage >= 100))
        {
            items.Add(new CuttableExpenseItem
            {
                Group = g.BudgetGroup,
                DisplayName = g.BudgetGroup.ToDisplayName(),
                MonthlyAverage = g.MonthlyAverage,
                TransactionCount = g.TransactionCount,
                GroupType = BudgetGroupType.SemiAdjustable
            });
        }

        return [.. items.OrderByDescending(i => i.MonthlyAverage)];
    }

    private static decimal CalculateSlope(List<decimal> values)
    {
        int n = values.Count;
        if (n < 2) return 0;

        decimal sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumX2 += i * i;
        }

        var denom = n * sumX2 - sumX * sumX;
        if (denom == 0) return 0;
        return (n * sumXY - sumX * sumY) / denom;
    }
}
