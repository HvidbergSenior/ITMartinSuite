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
    private readonly BudgetDbContext _db;

    public FamilyPlanningService(
        BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<FamilyPlanningViewModel>
    BuildAsync()
{
    var transactions =
        await _db.Transactions
            .AsNoTracking()
            .ToListAsync();

    var months =
        Math.Max(
            1,
            GetMonthsCovered(transactions));

    var incomeGroups =
        transactions
            .Where(x => x.Amount > 0)
            .GroupBy(x => x.BudgetGroup)
            .Select(x => new FamilyIncomeCategory
            {
                Name =
                    x.Key.ToDisplayName(),

                MonthlyAmount =
                    x.Sum(y => y.Amount)
                    / months,

                Transactions =
                    x.OrderByDescending(y => y.Date)
                     .ToList()
            })
            .OrderByDescending(x =>
                x.MonthlyAmount)
            .ToList();

    var monthlyIncome =
        incomeGroups.Sum(x =>
            x.MonthlyAmount);

    var budgetGroups =
        transactions
            .Where(x => x.Amount < 0)
            .GroupBy(x => x.BudgetGroup)
            .Select(x =>
            {
                var monthlyAmount =
                    Math.Abs(
                        x.Sum(y => y.Amount))
                    / months;

                return new FamilyBudgetGroup
                {
                    Name =
                        x.Key.ToDisplayName(),

                    MonthlyAmount =
                        monthlyAmount,

                    PercentOfIncome =
                        monthlyIncome == 0
                            ? 0
                            : monthlyAmount
                              / monthlyIncome
                              * 100,

                    Priority =
                        BudgetGroupPriority.Flexible,

                    PotentialReduction =
                        monthlyAmount * 0.25m,

                    Transactions =
                        x.OrderByDescending(y => y.Date)
                         .ToList()
                };
            })
            .OrderByDescending(x =>
                x.MonthlyAmount)
            .ToList();

    return new FamilyPlanningViewModel
    {
        CurrentMonthlyIncome =
            monthlyIncome,

        FutureMonthlyIncome =
            monthlyIncome,

        IncomeCategories =
            incomeGroups,

        BudgetGroups =
            budgetGroups,

        PotentialSavings =
            budgetGroups.Sum(x =>
                x.PotentialReduction)
    };
}

    private static int GetMonthsCovered(
        List<BankTransaction> transactions)
    {
        if (!transactions.Any())
        {
            return 1;
        }

        var min =
            transactions.Min(x => x.Date);

        var max =
            transactions.Max(x => x.Date);

        return ((max.Year - min.Year) * 12)
               + max.Month
               - min.Month
               + 1;
    }
}