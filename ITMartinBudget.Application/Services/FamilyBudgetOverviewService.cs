using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class FamilyBudgetOverviewService
    : IFamilyBudgetOverviewService
{
    public FamilyBudgetOverview Build2025Overview(
        List<BankTransaction> transactions)
    {
        return BuildOverview(
            transactions,
            "2025 Overview",
            new DateTime(2025, 1, 1),
            new DateTime(2025, 12, 31),
            0);
    }

    public FamilyBudgetOverview Build2026FirstHalfOverview(
        List<BankTransaction> transactions)
    {
        return BuildOverview(
            transactions,
            "2026 First Half",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 6, 30),
            0);
    }

    public FamilyBudgetOverview Build2026SecondHalfOverview(
        List<BankTransaction> transactions)
    {
        // TODO:
        // Replace with actual new salary.

        const decimal newMonthlyIncome = 68000;

        var overview = BuildOverview(
            transactions,
            "2026 Second Half Projection",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 6, 30),
            6);

        overview.MonthlyIncome =
            newMonthlyIncome;

        overview.ExpectedRemainingPeriod =
            overview.MonthlyRemaining
            * overview.MonthsRemaining;

        return overview;
    }

    private FamilyBudgetOverview BuildOverview(
        List<BankTransaction> transactions,
        string title,
        DateTime from,
        DateTime to,
        int remainingMonths)
    {
        var filtered = transactions
            .Where(x =>
                x.Date >= from
                && x.Date <= to)
            .ToList();

        if (!filtered.Any())
        {
            return new FamilyBudgetOverview
            {
                Title = title
            };
        }

        var months =
            Math.Max(
                1,
                ((to.Year - from.Year) * 12)
                + to.Month
                - from.Month
                + 1);

        var monthlyIncome =
            filtered
                .Where(x =>
                    x.TransactionType ==
                    TransactionType.Indkomst

                    &&

                    x.BudgetGroup !=
                    BudgetGroup.InternalTransfer

                    &&

                    x.BudgetGroup !=
                    BudgetGroup.Refund)
                .Sum(x => x.Amount)
            / months;

        var monthlyFixedExpenses =
            Math.Abs(
                filtered
                    .Where(x =>
                        x.TransactionType ==
                        TransactionType.Udgift

                        &&

                        (
                            x.BudgetGroup ==
                            BudgetGroup.FixedExpense

                            ||

                            x.IsRecurring
                        ))
                    .Sum(x => x.Amount)
            ) / months;

        var monthlyVariableExpenses =
            Math.Abs(
                filtered
                    .Where(x =>
                        x.TransactionType ==
                        TransactionType.Udgift

                        &&

                        x.BudgetGroup !=
                        BudgetGroup.FixedExpense

                        &&

                        !x.IsRecurring

                        &&

                        x.BudgetGroup !=
                        BudgetGroup.InternalTransfer)
                    .Sum(x => x.Amount)
            ) / months;

        return new FamilyBudgetOverview
        {
            Title = title,

            MonthlyIncome =
                monthlyIncome,

            MonthlyFixedExpenses =
                monthlyFixedExpenses,

            MonthlyVariableExpenses =
                monthlyVariableExpenses,

            MonthsRemaining =
                remainingMonths,

            ExpectedRemainingPeriod =
                (
                    monthlyIncome
                    - monthlyFixedExpenses
                    - monthlyVariableExpenses
                )
                * remainingMonths
        };
    }
}