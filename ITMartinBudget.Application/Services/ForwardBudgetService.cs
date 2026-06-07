using ITMartinBudget.Application.Extensions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public sealed class ForwardBudgetService
    : IForwardBudgetService
{
    private readonly IDashboardService _dashboardService;

    public ForwardBudgetService(
        IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<ForwardBudgetViewModel> BuildAsync()
    {
        var dashboard =
            await _dashboardService.BuildDashboardAsync();

        var transactions =
            dashboard.Transactions;

        var model =
            new ForwardBudgetViewModel();

        BuildIncome(
            model,
            transactions);

        BuildMandatoryExpenses(
            model,
            transactions);

        BuildRecurringAdjustableExpenses(
            model,
            transactions);
        BuildAdjustableGroups(
            model,
            transactions);

        model.AdjustableMonthlyExpenses =
            model.AdjustableGroups.Sum(
                x => x.Last3MonthsAverage);

        return model;
    }

    private static void BuildIncome(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        var incomes =
            transactions
                .Where(x =>
                    x.BudgetGroup.IsFixedIncome())
                .Where(x =>
                    x.Date >= DateTime.Today.AddMonths(-2))
                .GroupBy(x =>
                    x.Title)
                .Select(x =>
                    new IncomeItemViewModel
                    {
                        Title = x.Key,
                        ExpectedAmount =
                            x.Min(y =>
                                Math.Abs(y.Amount))
                    })
                .OrderByDescending(x =>
                    x.ExpectedAmount)
                .ToList();

        model.IncomeItems =
            incomes;

        model.ExpectedMonthlyIncome =
            incomes.Sum(x =>
                x.ExpectedAmount);
    }

    private static void BuildFixedExpenses(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        var fixedExpenses =
            transactions
                .Where(x =>
                    x.BudgetGroup.IsMandatoryExpense())
                .GroupBy(x =>
                    x.Title)
                .Select(x =>
                    new FixedExpenseViewModel
                    {
                        Title = x.Key,

                        MonthlyAmount =
                            Math.Abs(
                                x.OrderByDescending(y =>
                                        y.Date)
                                    .First()
                                    .Amount),

                        RecurringIntervalMonths =
                            x.Max(y =>
                                y.RecurringIntervalMonths)
                    })
                .OrderByDescending(x =>
                    x.MonthlyAmount)
                .ToList();

        model.FixedExpenses =
            fixedExpenses;

        model.FixedMonthlyExpenses =
            fixedExpenses.Sum(x =>
                x.MonthlyAmount);
    }

    private static void BuildAdjustableGroups(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        var now =
            DateTime.Today;

        model.AdjustableGroups =
            transactions
                .Where(x =>
                    x.BudgetGroup.IsAdjustable())
                .GroupBy(x =>
                    x.BudgetGroup)
                .Select(x =>
                {
                    var items =
                        x.ToList();

                    return new AdjustableBudgetGroupViewModel
                    {
                        BudgetGroup = x.Key,

                        LastMonthAmount =
                            Math.Abs(
                                items
                                    .Where(y =>
                                        y.Date >= now.AddMonths(-1))
                                    .Sum(y =>
                                        y.Amount)),

                        Last3MonthsAverage =
                            Math.Abs(
                                items
                                    .Where(y =>
                                        y.Date >= now.AddMonths(-3))
                                    .Sum(y =>
                                        y.Amount) / 3m),

                        Last12MonthsAverage =
                            Math.Abs(
                                items
                                    .Where(y =>
                                        y.Date >= now.AddMonths(-12))
                                    .Sum(y =>
                                        y.Amount) / 12m),

                        CurrentYearTotal =
                            Math.Abs(
                                items
                                    .Where(y =>
                                        y.Date.Year == now.Year)
                                    .Sum(y =>
                                        y.Amount)),

                        TransactionCount =
                            items.Count
                    };
                })
                .OrderByDescending(x =>
                    x.CurrentYearTotal)
                .ToList();
    }
    private static void BuildMandatoryExpenses(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        var expenses =
            transactions
                .Where(x =>
                    x.BudgetGroup
                        .IsMandatoryExpense())
                .GroupBy(x =>
                    x.Title)
                .Select(x =>
                {
                    var latest =
                        x.OrderByDescending(y =>
                                y.Date)
                            .First();

                    return new FixedExpenseViewModel
                    {
                        Title = x.Key,

                        MonthlyAmount =
                            Math.Abs(
                                latest.Amount) /
                            Math.Max(
                                1,
                                latest.RecurringIntervalMonths),

                        RecurringIntervalMonths =
                            latest.RecurringIntervalMonths
                    };
                })
                .OrderByDescending(x =>
                    x.MonthlyAmount)
                .ToList();

        model.FixedExpenses =
            expenses;

        model.FixedMonthlyExpenses =
            expenses.Sum(x =>
                x.MonthlyAmount);
    }
    private static void BuildRecurringAdjustableExpenses(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        model.RecurringAdjustableExpenses =
            transactions
                .Where(x =>
                    x.BudgetGroup
                        .IsRecurringAdjustable())
                .GroupBy(x =>
                    x.Title)
                .Select(x =>
                {
                    var latest =
                        x.OrderByDescending(y =>
                                y.Date)
                            .First();

                    return new FixedExpenseViewModel
                    {
                        Title = x.Key,

                        MonthlyAmount =
                            Math.Abs(
                                latest.Amount) /
                            Math.Max(
                                1,
                                latest.RecurringIntervalMonths),

                        RecurringIntervalMonths =
                            latest.RecurringIntervalMonths
                    };
                })
                .OrderByDescending(x =>
                    x.MonthlyAmount)
                .ToList();
    }
}