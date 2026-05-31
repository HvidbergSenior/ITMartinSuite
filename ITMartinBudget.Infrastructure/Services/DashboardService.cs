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
        var transactions =
            await _db.Transactions
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
                    x.BudgetGroup != BudgetGroup.InternalTransfer)
                .Sum(x => x.Amount);

        var totalExpenses =
            Math.Abs(
                transactions
                    .Where(x =>
                        x.Amount < 0 &&
                        x.BudgetGroup != BudgetGroup.InternalTransfer)
                    .Sum(x => x.Amount));

        var internalTransferIncome =
            transactions
                .Where(x =>
                    x.BudgetGroup ==
                    BudgetGroup.InternalTransfer &&
                    x.Amount > 0)
                .Sum(x => x.Amount);

        var internalTransferExpenses =
            Math.Abs(
                transactions
                    .Where(x =>
                        x.BudgetGroup ==
                        BudgetGroup.InternalTransfer &&
                        x.Amount < 0)
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

            FixedExpenses =
                Math.Abs(
                    transactions
                        .Where(x =>
                            x.BudgetGroup ==
                            BudgetGroup.FixedExpense)
                        .Sum(x => x.Amount)),

            InternalTransferIncome =
                internalTransferIncome,

            InternalTransferExpenses =
                internalTransferExpenses,

            InternalTransferNet =
                internalTransferIncome -
                internalTransferExpenses,

            UncategorizedTransactions =
                transactions
                    .Where(x =>
                        x.BudgetGroup ==
                        BudgetGroup.Uncategorized)
                    .ToList(),

            BudgetGroupSummaries =
                transactions

                    .Where(x =>
                        x.BudgetGroup !=
                        BudgetGroup.InternalTransfer)

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
}