using System.Globalization;
using ITMartinBudget.Domain;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Services;

public class BudgetService : IBudgetService
{
    public IEnumerable<CategorySummary> GetSummary(
        List<BankTransaction> transactions,
        int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.Category != Category.Transfer &&
                x.Category != Category.Savings);

        return filtered
            .GroupBy(x => x.Category)
            .Select(group => new CategorySummary
            {
                Category = group.Key,

                DisplayName = group.Key.ToString(),

                Income = group
                    .Where(x => x.Amount > 0)
                    .Sum(x => x.Amount),

                Expenses = group
                    .Where(x => x.Amount < 0)
                    .Sum(x => Math.Abs(x.Amount)),

                TransactionCount = group.Count()
            })
            .OrderByDescending(x => x.Income + x.Expenses);
    }

    public YearSummary GetYearTotals(
        List<BankTransaction> transactions,
        int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year);

        return new YearSummary
        {
            Income = filtered
                .Where(x =>
                    x.TransactionType == TransactionType.Indkomst)
                .Where(x =>
                    x.Category != Category.Transfer)
                .Sum(x => x.Amount),

            Expenses = filtered
                .Where(x =>
                    x.TransactionType == TransactionType.Udgift)
                .Where(x =>
                    x.Category != Category.Transfer &&
                    x.Category != Category.Savings)
                .Sum(x => Math.Abs(x.Amount))
        };
    }

    public IEnumerable<BudgetOverviewItem> GetBudgetOverview(
        List<BankTransaction> transactions,
        int year)
    {
        return transactions
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.TransactionType != TransactionType.Overførsel)
            .GroupBy(x => new
            {
                x.TransactionType,
                x.Category
            })
            .Select(group => new BudgetOverviewItem
            {
                Title = group.Key.Category.ToString(),

                TransactionType = group.Key.TransactionType,

                Total = group
                    .Sum(x => Math.Abs(x.Amount)),

                MonthlyAverage =
                    group.Sum(x => Math.Abs(x.Amount)) / 12m,

                Transactions = group
                    .OrderByDescending(x => x.Date)
                    .ToList()
            })
            .OrderByDescending(x => x.Total);
    }

    public IEnumerable<MonthlyBudgetSummary>
        GetMonthlySummaries(
            List<BankTransaction> transactions,
            int year)
    {
        return transactions
            .Where(x => x.Date.Year == year)
            .GroupBy(x => new
            {
                x.Date.Year,
                x.Date.Month
            })
            .Select(g => new MonthlyBudgetSummary
            {
                Year = g.Key.Year,

                Month = g.Key.Month,

                MonthName =
                    new DateTime(
                            g.Key.Year,
                            g.Key.Month,
                            1)
                        .ToString("MMMM"),

                Income = g
                    .Where(x =>
                        x.TransactionType ==
                        TransactionType.Indkomst)
                    .Where(x =>
                        x.Category != Category.Transfer)
                    .Sum(x => x.Amount),

                Expenses = g
                    .Where(x =>
                        x.TransactionType ==
                        TransactionType.Udgift)
                    .Where(x =>
                        x.Category != Category.Transfer &&
                        x.Category != Category.Savings)
                    .Sum(x => Math.Abs(x.Amount))
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month);
    }

    private TransactionFrequency DetectFrequency(
        List<BankTransaction> transactions)
    {
        var months = transactions
            .Select(x => new
            {
                x.Date.Year,
                x.Date.Month
            })
            .Distinct()
            .Count();

        return months switch
        {
            >= 10 => TransactionFrequency.Monthly,
            >= 4 => TransactionFrequency.Quarterly,
            >= 2 => TransactionFrequency.Irregular,
            _ => TransactionFrequency.OneTime
        };
    }
}