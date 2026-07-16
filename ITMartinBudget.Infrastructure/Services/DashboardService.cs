using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class DashboardService
    : IDashboardService
{
    private readonly BudgetDbContext _db;

    public DashboardService(
        BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardViewModel>
        BuildDashboardAsync()
    {
        // This service is the family budget dashboard specifically - a client
        // ledger like "bogshoppen" (see ShopOverview.razor, which queries
        // Db.Transactions directly and scopes by its own LedgerId) must never
        // show up here.
        var transactions =
            await _db.Transactions
                .Where(x => x.LedgerId == "family")
                .ToListAsync();

        var firstDate =
            transactions.MinBy(x => x.Date)?.Date;

        var lastDate =
            transactions.MaxBy(x => x.Date)?.Date;

        var monthsLoaded =
            CalculateMonths(
                firstDate,
                lastDate);

        var totalIncome =
            transactions
                .Where(x =>
                    x.Amount > 0 &&
                    !IsExcludedFromDashboard(x.BudgetGroup))
                .Sum(x => x.Amount);

        var totalExpenses =
            Math.Abs(
                transactions
                    .Where(x =>
                        x.Amount < 0 &&
                        !IsExcludedFromDashboard(x.BudgetGroup))
                    .Sum(x => x.Amount));
        
        return new DashboardViewModel
        {
            Transactions = transactions,
            FirstTransactionDate = firstDate,
            LastTransactionDate = lastDate,
            MonthsLoaded = monthsLoaded,

            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetAmount = totalIncome - totalExpenses,

            FixedIncome =
                transactions
                    .Where(x =>
                        x.BudgetGroup ==
                        BudgetGroup.FixedIncome)
                    .Sum(x => x.Amount),
            
            UncategorizedTransactions =
                transactions
                    .Where(x =>
                        x.BudgetGroup ==
                        BudgetGroup.Uncategorized)
                    .ToList(),

            BudgetGroupSummaries =
                transactions
                    .Where(x =>
                        !IsExcludedFromDashboard(
                            x.BudgetGroup))
                    .GroupBy(x => x.BudgetGroup)
                    .Select(x =>
                        new BudgetGroupSummary
                        {
                            BudgetGroup = x.Key,

                            Income =
                                x.Where(y => y.Amount > 0)
                                    .Sum(y => y.Amount),

                            Expenses =
                                Math.Abs(
                                    x.Where(y => y.Amount < 0)
                                        .Sum(y => y.Amount)),

                            TransactionCount =
                                x.Count()
                        })
                    .OrderByDescending(x =>
                        Math.Abs(x.Total))
                    .ToList()
        };
    }

    private static int CalculateMonths(
        DateTime? first,
        DateTime? last)
    {
        if (!first.HasValue ||
            !last.HasValue)
        {
            return 1;
        }

        return Math.Max(
            1,
            ((last.Value.Year -
              first.Value.Year) * 12)
            + last.Value.Month
            - first.Value.Month
            + 1);
    }
    private static bool IsExcludedFromDashboard(
        BudgetGroup budgetGroup)
    {
        return budgetGroup is
            BudgetGroup.OverførslerTilFraOpsparingsKonto;
    }
}