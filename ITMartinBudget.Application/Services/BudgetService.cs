using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class BudgetService
{
    public IEnumerable<CategorySummary> GetSummary(List<BankTransaction> transactions, int year)
    {
        var filtered = transactions
            .Where(x => x.Date.Year == year)
            .ToList();

        var grouped = filtered
            .GroupBy(x => new
            {
                x.MainCategory,
                Sub = x.MobilePayName ?? x.SubCategory.ToString()
            });
        var result = new List<CategorySummary>();

        foreach (var group in grouped)
        {
            var income = group
                .Where(x => x.Amount > 0)
                .Sum(x => Math.Abs(x.Amount));

            var expenses = group
                .Where(x => x.Amount < 0)
                .Sum(x => -Math.Abs(x.Amount));
            
            result.Add(new CategorySummary
            {
                MainCategory = group.Key.MainCategory,
                SubCategory = SubCategory.Ukendt, // not used anymore for display

                DisplayName = group.Key.Sub,

                Total = income + expenses,
                Income = income,
                Expenses = expenses,

                TransactionCount = group.Count(),

                Frequency = DetectFrequency(group.Select(x => x.Date).ToList()),

                ExpenseType = GetDefaultExpenseType(group.Key.MainCategory, SubCategory.Ukendt)
            });
        }

        return result
            .OrderByDescending(x => Math.Abs(x.Total));
    }
    
    public YearSummary GetYearTotals(List<BankTransaction> transactions, int year)
    {
        var filtered = transactions.Where(x => x.Date.Year == year);

        var income = filtered.Where(x => x.Amount > 0).Sum(x => x.Amount);
        var expenses = filtered.Where(x => x.Amount < 0).Sum(x => x.Amount);

        return new YearSummary
        {
            Income = income,
            Expenses = expenses
        };
    }

    private TransactionFrequency DetectFrequency(List<DateTime> dates)
    {
        if (dates.Count < 2)
            return TransactionFrequency.OneTime;

        dates = dates.OrderBy(x => x).ToList();

        var diffs = new List<int>();

        for (int i = 1; i < dates.Count; i++)
        {
            diffs.Add((dates[i] - dates[i - 1]).Days);
        }

        var avg = diffs.Average();

        if (avg <= 7) return TransactionFrequency.Weekly;
        if (avg <= 31) return TransactionFrequency.Monthly;
        if (avg <= 100) return TransactionFrequency.Quarterly; // 🔥 your addition
        if (avg <= 370) return TransactionFrequency.Yearly;

        return TransactionFrequency.Irregular;
    }

    private ExpenseType GetDefaultExpenseType(MainCategory main, SubCategory sub)
    {
        // NEED (fixed costs)
        if (main == MainCategory.Bolig)
            return ExpenseType.Need;

        if (sub == SubCategory.Dagligvarer)
            return ExpenseType.Need;

        if (sub == SubCategory.Benzin || sub == SubCategory.Parkering)
            return ExpenseType.Need;

        if (sub == SubCategory.Husleje)
            return ExpenseType.Need;

        if (sub == SubCategory.Sundhed)
            return ExpenseType.Need;

        // NICE (flexible)
        if (sub == SubCategory.Restaurant ||
            sub == SubCategory.Underholdning ||
            sub == SubCategory.Tøj)
            return ExpenseType.Nice;

        return ExpenseType.Need;
    }
}