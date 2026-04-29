using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class BudgetService
{
    public IEnumerable<CategorySummary> GetSummary(List<BankTransaction> transactions, int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.SubCategory != SubCategory.OverførselFraAndre &&
                x.SubCategory != SubCategory.OverførselTilAndre)
            .ToList();

        var grouped = filtered
            .GroupBy(x => new
            {
                x.Category,
                x.SubCategory
            });

        var result = new List<CategorySummary>();

        foreach (var group in grouped)
        {
            var income = group.Where(x => x.Amount > 0).Sum(x => x.Amount);
            var expenses = group.Where(x => x.Amount < 0).Sum(x => Math.Abs(x.Amount));

            result.Add(new CategorySummary
            {
                Category = group.Key.Category,
                SubCategory = group.Key.SubCategory,
                DisplayName = GetDisplayName(group.Key.SubCategory),

                Income = income,
                Expenses = expenses,

                TransactionCount = group.Count(),
                Frequency = DetectFrequency(group.Select(x => x.Date).ToList()),

                // 🔥 NEW
                ExpenseType = GetExpenseType(group.Key.SubCategory)
            });
        }

        return result
            .OrderByDescending(x => x.Income + x.Expenses);
    }

    public YearSummary GetYearTotals(List<BankTransaction> transactions, int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x =>
                x.SubCategory != SubCategory.OverførselFraAndre &&
                x.SubCategory != SubCategory.OverførselTilAndre);

        return new YearSummary
        {
            Income = filtered.Where(x => x.Amount > 0).Sum(x => x.Amount),

            // 🔥 always positive
            Expenses = filtered.Where(x => x.Amount < 0)
                               .Sum(x => Math.Abs(x.Amount))
        };
    }

    private string GetDisplayName(SubCategory sub) => sub switch
    {
        // 💰 INCOME
        SubCategory.Løn => "Løn",
        SubCategory.SU => "SU",
        SubCategory.OverskydendeSkat => "Overskydende skat",
        SubCategory.Renter => "Renter",
        SubCategory.Pengegaver => "Gaver (ind)",

        // 🛒 FOOD
        SubCategory.Dagligvarer => "Dagligvarer",
        SubCategory.Restaurant => "Restaurant",
        SubCategory.Snacks => "Snacks",

        // 🚗 TRANSPORT
        SubCategory.Benzin => "Benzin",
        SubCategory.Parkering => "Parkering",
        SubCategory.ReparationBil => "Bil reparation",
        SubCategory.OffentligTransport => "Offentlig transport",

        // 🏠 HOUSING
        SubCategory.Husleje => "Husleje",
        SubCategory.RenterLån => "Renter lån",
        SubCategory.VarmeVandAffald => "Varme/Vand/Affald",
        SubCategory.ReparationHus => "Hus reparation",

        // 📱 SUBSCRIPTIONS
        SubCategory.Telefonabonnement => "Telefon",
        SubCategory.Internet => "Internet",
        SubCategory.StreamingTjenester => "Streaming",

        // 🏥 HEALTH
        SubCategory.Tandlæge => "Tandlæge",
        SubCategory.Sygeforsikring => "Sygeforsikring",
        SubCategory.Medicin => "Medicin",

        // 👕 SHOPPING
        SubCategory.Tøj => "Tøj",

        // 🎮 LEISURE
        SubCategory.Underholdning => "Underholdning",
        SubCategory.FitnessSport => "Fitness/Sport",
        SubCategory.Rejser => "Rejser",

        // 📊 FINANCE
        SubCategory.FagforeningAkasse => "Fagforening/A-kasse",
        SubCategory.Forsikring => "Forsikring",
        SubCategory.VirksomhedsUdgift => "Virksomhed",

        // ✂️ PERSONAL
        SubCategory.Frisør => "Frisør",
        SubCategory.PersonligPleje => "Pleje",
        SubCategory.GaverTilAndre => "Gaver (ud)",

        // 💾 SAVINGS
        SubCategory.Opsparing => "Opsparing",
        SubCategory.Børneopsparing => "Børneopsparing",

        _ => sub.ToString()
    };

    private ExpenseType GetExpenseType(SubCategory sub)
    {
        return sub switch
        {
            // 🏠 FIXED
            SubCategory.Husleje or
            SubCategory.RenterLån or
            SubCategory.Forsikring or
            SubCategory.FagforeningAkasse or
            SubCategory.Telefonabonnement or
            SubCategory.Internet or
            SubCategory.StreamingTjenester
                => ExpenseType.Fixed,

            // 🛒 VARIABLE
            SubCategory.Dagligvarer or
            SubCategory.Benzin or
            SubCategory.Parkering or
            SubCategory.Medicin
                => ExpenseType.Variable,

            // 🎮 OPTIONAL
            SubCategory.Restaurant or
            SubCategory.Snacks or
            SubCategory.Underholdning or
            SubCategory.FitnessSport or
            SubCategory.Rejser or
            SubCategory.GaverTilAndre or
            SubCategory.Frisør or
            SubCategory.PersonligPleje
                => ExpenseType.Optional,

            _ => ExpenseType.Variable
        };
    }

    private TransactionFrequency DetectFrequency(List<DateTime> dates)
    {
        if (dates.Count < 2)
            return TransactionFrequency.OneTime;

        dates = dates.OrderBy(x => x).ToList();

        var diffs = new List<int>();

        for (int i = 1; i < dates.Count; i++)
            diffs.Add((dates[i] - dates[i - 1]).Days);

        if (!diffs.Any())
            return TransactionFrequency.OneTime;

        var sorted = diffs.OrderBy(x => x).ToList();
        var middle = sorted.Skip(sorted.Count / 4).Take(sorted.Count / 2).ToList();

        var avg = middle.Any() ? middle.Average() : sorted.Average();

        if (avg <= 7) return TransactionFrequency.Weekly;
        if (avg <= 31) return TransactionFrequency.Monthly;
        if (avg <= 100) return TransactionFrequency.Quarterly;
        if (avg <= 370) return TransactionFrequency.Yearly;

        return TransactionFrequency.Irregular;
    }
}