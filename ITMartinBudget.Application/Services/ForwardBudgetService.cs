using ITMartinBudget.Application.Extensions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

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

        BuildSemiAdjustableGroups(model, transactions);
        BuildIgnoredGroups(
            model,
            transactions);
        model.AdjustableMonthlyExpenses =
            model.AdjustableGroups.Sum(
                x => x.Last3MonthsAverage);

        return model;
    }
    private static void BuildIgnoredGroups(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        model.IgnoredGroups =
            BuildBudgetGroups(
                transactions,
                BudgetGroupType.Ignore);
    }
    private static void BuildIncome(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        var incomes =
            transactions
                .Where(x =>
                    x.BudgetGroup.GetBudgetGroupType() ==
                    BudgetGroupType.FixedIncome)
                .Where(x =>
                    !string.Equals(
                        x.Title,
                        "AutoproffLøn",
                        StringComparison.OrdinalIgnoreCase))
                .GroupBy(x =>
                    x.Title)
                .Select(x =>
                    new IncomeItemViewModel
                    {
                        Title = x.Key,

                        ExpectedAmount =
                            Math.Abs(
                                x.OrderByDescending(y =>
                                        y.Date)
                                    .First()
                                    .Amount)
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

    

    private static void BuildAdjustableGroups(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        model.AdjustableGroups =
            BuildBudgetGroups(
                transactions,
                BudgetGroupType.Adjustable);
    }
    private static void BuildMandatoryExpenses(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        var expenses =
            transactions
                .Where(x =>
                    x.BudgetGroup.GetBudgetGroupType() ==
                    BudgetGroupType.MandatoryExpense)
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
                    x.BudgetGroup.GetBudgetGroupType() ==
                    BudgetGroupType.RecurringAdjustable)
                .GroupBy(x =>
                    x.Title)
                .Select(x =>
                {
                    var latestMonth =
                        x.Max(y =>
                            new DateTime(
                                y.Date.Year,
                                y.Date.Month,
                                1));

                    var monthTotal =
                        Math.Abs(
                            x.Where(y =>
                                    y.Date.Year ==
                                    latestMonth.Year &&
                                    y.Date.Month ==
                                    latestMonth.Month)
                                .Sum(y =>
                                    y.Amount));

                    var latest =
                        x.OrderByDescending(y =>
                                y.Date)
                            .First();

                    return new FixedExpenseViewModel
                    {
                        Title = x.Key,

                        MonthlyAmount =
                            monthTotal /
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
    private static void BuildSemiAdjustableGroups(
        ForwardBudgetViewModel model,
        IEnumerable<BankTransaction> transactions)
    {
        model.SemiAdjustableGroups =
            BuildBudgetGroups(
                transactions,
                BudgetGroupType.SemiAdjustable);
    }
    private static List<AdjustableBudgetGroupViewModel>
    BuildBudgetGroups(
        IEnumerable<BankTransaction> transactions,
        BudgetGroupType budgetGroupType)
{
    var now =
        DateTime.Today;

    return transactions
        .Where(x =>
            x.BudgetGroup.GetBudgetGroupType() ==
            budgetGroupType)
        .GroupBy(x =>
            x.BudgetGroup)
        .Select(x =>
        {
            var items =
                x.ToList();

            return new AdjustableBudgetGroupViewModel
            {
                BudgetGroup =
                    x.Key,

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
                    items.Count,

                RecentTransactions =
                    items
                        .OrderByDescending(y =>
                            y.Date)
                        .Take(10)
                        .Select(y =>
                            new TransactionSummaryViewModel
                            {
                                Date = y.Date,
                                Title = y.Title,
                                Amount = Math.Abs(y.Amount)
                            })
                        .ToList()
            };
        })
        .OrderByDescending(x =>
            x.Last3MonthsAverage)
        .ToList();
}
}