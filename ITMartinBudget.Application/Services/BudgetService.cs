using ITMartinBudget.Domain;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Services;

public class BudgetService : IBudgetService
{
    // =====================================
    // CATEGORY SUMMARY
    // =====================================

    public IEnumerable<CategorySummary> GetSummary(
        List<BankTransaction> transactions,
        int year)
    {
        var filtered = transactions

            .Where(x =>
                x.Date.Year == year);

        return filtered

            .GroupBy(x => x.Category)

            .Select(group => new CategorySummary
            {
                Category = group.Key,

                DisplayName =
                    GetCategoryTitle(
                        group.Key),

                Income = group
                    .Where(x => x.Amount > 0)
                    .Sum(x => x.Amount),

                Expenses = group
                    .Where(x => x.Amount < 0)
                    .Sum(x => Math.Abs(x.Amount)),

                TransactionCount =
                    group.Count()
            })

            .OrderByDescending(x =>
                x.Income + x.Expenses);
    }

    // =====================================
    // RAW CASHFLOW TOTALS
    // =====================================

    public YearSummary GetYearTotals(
        List<BankTransaction> transactions,
        int year)
    {
        var filtered = transactions

            .Where(x =>
                x.Date.Year == year);

        return new YearSummary
        {
            Income = filtered

                .Where(x =>
                    x.Amount > 0)

                .Sum(x =>
                    x.Amount),

            Expenses = filtered

                .Where(x =>
                    x.Amount < 0)

                .Sum(x =>
                    Math.Abs(x.Amount))
        };
    }

    // =====================================
    // BUDGET OVERVIEW
    // =====================================

    public IEnumerable<BudgetOverviewItem>
        GetBudgetOverview(
            List<BankTransaction> transactions,
            int year)
    {
        return transactions

            .Where(x =>
                x.Date.Year == year)

            .GroupBy(x => new
            {
                x.Title,
                x.BudgetGroup
            })

            .Select(group =>
                new BudgetOverviewItem
                {
                    Title =
                        group.Key.Title,

                    BudgetGroup =
                        group.Key.BudgetGroup,

                    Category =
                        group.First().Category,

                    TransactionType =
                        group.First().TransactionType,

                    Total =
                        group.Sum(x => x.Amount),

                    Transactions =
                        group
                            .OrderByDescending(x => x.Date)
                            .ToList()
                })

            .OrderByDescending(x =>
                Math.Abs(x.Total))

            .ToList();
    }

    // =====================================
    // CATEGORY TITLES
    // =====================================

    private static string GetCategoryTitle(
        Category category)
    {
        return category switch
        {
            Category.Løn => "Indkomst",

            Category.BoligVedligehold => "Bolig",
            Category.Regninger => "Regninger",
            Category.Forsikring => "Forsikring",
            Category.TelefonTvInternet => "Telefon / TV / Internet",
            Category.Pension => "Pension",

            Category.Opsparing => "Opsparing",

            Category.Overfoersel => "Overførsel",
            Category.OverfoerselTilBertil => "Overførsel til Bertil",
            Category.OverfoerselTilEigil => "Overførsel til Eigil",
            Category.OverfoerselTilJulius => "Overførsel til Julius",

            Category.FagforeningAKasse => "Fagforening / A-kasse",

            Category.Dagligvarer => "Dagligvarer",
            Category.Takeaway => "Takeaway",
            Category.Restaurant => "Restaurant",
            Category.Cafe => "Cafe",

            Category.Streaming => "Streaming",
            Category.KoncertBio => "Koncert",
            Category.Gaming => "Gaming",
            Category.Apps => "Apps",
            Category.Northside => "NorthSide",
            Category.Fritid => "Fritid",

            Category.Parkering => "Parkering",
            Category.Braendstof => "Brændstof",
            Category.OffentligTransport => "Offentlig transport",
            Category.BilVedligehold => "Bil vedligehold",

            Category.Toej => "Tøj",
            Category.Elektronik => "Elektronik",
            Category.Bolig => "Hjem",

            Category.Sundhed => "Sundhed",

            Category.Boern => "Børn",
            Category.Kaeledyr => "Kæledyr",
            Category.Gaver => "Gaver",

            Category.RejserUdflugter => "Rejse",

            Category.Andet => "Andet",

            _ => "Andet"
        };
    }
}