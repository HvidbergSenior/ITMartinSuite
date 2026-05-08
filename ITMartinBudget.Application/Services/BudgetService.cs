using System.Globalization;
using System.Text.RegularExpressions;
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
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.Category != Category.Transfer &&
                x.Category != Category.Savings);

        return new YearSummary
        {
            Income = filtered
                .Where(x => x.Amount > 0)
                .Sum(x => x.Amount),

            Expenses = filtered
                .Where(x => x.Amount < 0)
                .Sum(x => Math.Abs(x.Amount))
        };
    }

    public IEnumerable<BudgetOverviewItem> GetBudgetOverview(
        List<BankTransaction> transactions,
        int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.Category != Category.Transfer &&
                x.Category != Category.Savings);

        return filtered
            .GroupBy(x => new
            {
                x.TransactionType,
                x.Category,
                Title = GetGroupTitle(x)
            })
            .Select(group =>
            {
                var txs = group
                    .OrderByDescending(x => x.Date)
                    .ToList();

                var total =
                    group.Key.TransactionType == TransactionType.Udgift
                        ? txs.Sum(x => Math.Abs(x.Amount))
                        : txs.Sum(x => x.Amount);

                return new BudgetOverviewItem
                {
                    Title = group.Key.Title,

                    Category = group.Key.Category,

                    TransactionType = group.Key.TransactionType,

                    Total = total,

                    MonthlyAverage = total,

                    Transactions = txs
                };
            })
            .OrderByDescending(x => x.Total);
    }

    public IEnumerable<MonthlyBudgetSummary>
        GetMonthlySummaries(
            List<BankTransaction> transactions,
            int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.Category != Category.Transfer &&
                x.Category != Category.Savings);

        return filtered
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
                    .Where(x => x.Amount > 0)
                    .Sum(x => x.Amount),

                Expenses = g
                    .Where(x => x.Amount < 0)
                    .Sum(x => Math.Abs(x.Amount))
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month);
    }

    private string GetGroupTitle(BankTransaction tx)
    {
        var text = tx.Description.ToLowerInvariant();

        if (text.Contains("løn") ||
            text.Contains("lønoverførsel") ||
            text.Contains("månedsløn") ||
            text.Contains("plusløn"))
        {
            return "Salary";
        }

        if (Regex.IsMatch(
                text,
                @"(?<![a-zæøå])su(?![a-zæøå])"))
        {
            return "SU";
        }

        if (text.Contains("spotify"))
            return "Spotify";

        if (text.Contains("netflix"))
            return "Netflix";

        if (text.Contains("telenor"))
            return "Telenor";

        if (text.Contains("alka"))
            return "Alka Forsikring";

        if (text.Contains("nrgi"))
            return "NRGi";

        if (text.Contains("føtex") ||
            text.Contains("foetex"))
            return "Føtex";

        if (text.Contains("netto"))
            return "Netto";

        if (text.Contains("rema"))
            return "Rema 1000";

        if (text.Contains("bilka"))
            return "Bilka";

        if (text.Contains("steam"))
            return "Steam";

        if (text.Contains("circle k"))
            return "Circle K";

        if (text.Contains("uno-x"))
            return "Uno-X";

        return tx.Category.ToString();
    }
}