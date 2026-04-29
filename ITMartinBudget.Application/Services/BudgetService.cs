using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class BudgetService
{
    public IEnumerable<CategorySummary> GetSummary(List<BankTransaction> transactions, int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x => x.SubCategory != SubCategory.Overførsel)
            //.Where(x => x.SubCategory != SubCategory.Ukendt) // optional
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

                ExpenseType = GetDefaultExpenseType(group.Key.Category, group.Key.SubCategory)
            });
        }

        return result
            // 🔥 Better sorting (impact-based)
            .OrderByDescending(x => x.Income + x.Expenses);
    }

    public YearSummary GetYearTotals(List<BankTransaction> transactions, int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .Where(x => x.SubCategory != SubCategory.Overførsel);

        return new YearSummary
        {
            Income = filtered.Where(x => x.Amount > 0).Sum(x => x.Amount),
            Expenses = filtered.Where(x => x.Amount < 0).Sum(x => x.Amount)
        };
    }

    private string GetDisplayName(SubCategory sub) => sub switch
    {
        SubCategory.Løn => "Løn",
        SubCategory.Dagligvarer => "Dagligvarer",
        SubCategory.Restaurant => "Restaurant",
        SubCategory.Benzin => "Benzin",
        SubCategory.Parkering => "Parkering",
        SubCategory.Husleje => "Husleje",
        SubCategory.Bolig => "Bolig",
        SubCategory.Abonnement => "Abonnement",
        SubCategory.Sundhed => "Sundhed",
        SubCategory.Tøj => "Tøj",
        SubCategory.Underholdning => "Underholdning",
        SubCategory.Fritid => "Fritid",
        _ => sub.ToString()
    };

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

        // 🔥 Improved (removes outliers)
        var sorted = diffs.OrderBy(x => x).ToList();
        var middle = sorted
            .Skip(sorted.Count / 4)
            .Take(sorted.Count / 2)
            .ToList();

        var avg = middle.Any() ? middle.Average() : sorted.Average();

        if (avg <= 7) return TransactionFrequency.Weekly;
        if (avg <= 31) return TransactionFrequency.Monthly;
        if (avg <= 100) return TransactionFrequency.Quarterly;
        if (avg <= 370) return TransactionFrequency.Yearly;

        return TransactionFrequency.Irregular;
    }

    private ExpenseType GetDefaultExpenseType(Category category, SubCategory sub)
    {
        if (category == Category.Bolig)
            return ExpenseType.Need;

        if (sub == SubCategory.Dagligvarer ||
            sub == SubCategory.Benzin ||
            sub == SubCategory.Parkering ||
            sub == SubCategory.Husleje ||
            sub == SubCategory.Sundhed)
            return ExpenseType.Need;

        if (sub == SubCategory.Restaurant ||
            sub == SubCategory.Underholdning ||
            sub == SubCategory.Tøj)
            return ExpenseType.Nice;

        return ExpenseType.Need;
    }
}