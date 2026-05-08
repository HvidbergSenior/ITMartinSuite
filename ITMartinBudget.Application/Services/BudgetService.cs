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
            .Where(x => x.Date.Year == year);

        return filtered
            .Where(x =>
                x.Category != Category.Transfer &&
                x.Category != Category.Savings)
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

                    TransactionType =
                        txs.First().TransactionType,

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

        // =====================================
        // FIXED INCOME
        // =====================================

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

        // =====================================
        // FOOD
        // =====================================

        if (text.Contains("su fdb") ||
            text.Contains("fdb") ||
            text.Contains("superbrugsen") ||
            text.Contains("superbrugs"))
        {
            return "SuperBrugsen";
        }

        if (text.Contains("brugsen"))
            return "Brugsen";

        if (text.Contains("føtex") ||
            text.Contains("foetex"))
        {
            return "Føtex";
        }

        if (text.Contains("netto"))
            return "Netto";

        if (text.Contains("rema"))
            return "Rema 1000";

        if (text.Contains("løvbjerg") ||
            text.Contains("loevbjerg"))
        {
            return "Løvbjerg";
        }

        if (text.Contains("bilka"))
            return "Bilka";

        // =====================================
        // FIXED EXPENSES
        // =====================================

        if (text.Contains("spotify"))
            return "Spotify";

        if (text.Contains("netflix"))
            return "Netflix";

        if (text.Contains("telenor"))
            return "Telenor";

        if (text.Contains("nrgi"))
            return "NRGi";

        if (text.Contains("jyske realkredit"))
            return "Jyske Realkredit";

        if (text.Contains("alka"))
            return "Alka Forsikring";

        if (text.Contains("allente"))
            return "Allente";

        if (text.Contains("letsikr"))
            return "Letsikring";

        if (text.Contains("fitness"))
            return "Fitness";

        if (text.Contains("kredsløb"))
            return "Kredsløb";

        if (text.Contains("aarhus vand"))
            return "Aarhus Vand";

        if (text.Contains("google one"))
            return "Google One";

        if (text.Contains("suno"))
            return "Suno";

        if (text.Contains("playstation"))
            return "PlayStation";

        if (text.Contains("jetbrains"))
            return "JetBrains";

        if (text.Contains("hog-hinnerup"))
            return "HOG Hinnerup";

        if (text.Contains("parcelforeningen"))
            return "Parcelforeningen";

        if (text.Contains("motoropkrævning") ||
            text.Contains("motorstyrelsen"))
        {
            return "Motoropkrævning";
        }

        if (text.Contains("akademikernes a-kasse") ||
            text.Contains("a-kasse"))
        {
            return "A-Kasse";
        }

        if (text.Contains("socialpædagogernes"))
            return "Fagforening";

        if (text.Contains("dmr"))
            return "DMR";

        // =====================================
        // TRANSPORT
        // =====================================

        if (text.Contains("circle k"))
            return "Circle K";

        if (text.Contains("uno-x"))
            return "Uno-X";

        if (text.Contains("q8"))
            return "Q8";

        if (text.Contains("ok"))
            return "OK";

        if (text.Contains("shell"))
            return "Shell";

        if (text.Contains("ingo"))
            return "Ingo";

        if (text.Contains("brobizz"))
            return "BroBizz";

        if (text.Contains("rejsekort"))
            return "Rejsekort";

        if (text.Contains("easypark"))
            return "EasyPark";

        if (text.Contains("sejer") ||
            text.Contains("mekaniker") ||
            text.Contains("værksted") ||
            text.Contains("autoservice"))
        {
            return "Mechanic";
        }

        // =====================================
        // ENTERTAINMENT
        // =====================================

        if (text.Contains("steam"))
            return "Steam";

        if (text.Contains("xbox"))
            return "Xbox";

        if (text.Contains("nintendo"))
            return "Nintendo";

        // =====================================
        // DEFAULT
        // =====================================

        return tx.Category.ToString();
    }
}