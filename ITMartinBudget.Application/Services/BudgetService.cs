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

        var grouped = filtered.GroupBy(x => x.Category?.Trim());
        
        var result = new List<CategorySummary>();

        foreach (var group in grouped)
        {
            var category = group.Key;

            var income = group
                .Where(x => x.Amount > 0)
                .Sum(x => x.Amount);

            var expenses = group
                .Where(x => x.Amount < 0)
                .Sum(x => x.Amount);

            result.Add(new CategorySummary
                {
                    Category = category,

                    Total = group.Sum(x => x.Amount),

                    Income = income,
                    Expenses = expenses,

                    TransactionCount = group.Count(),

                    Frequency = DetectFrequency(group.Select(x => x.Date).ToList()),

                    // ✅ NEW
                    ExpenseType = GetDefaultExpenseType(category)
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
    private ExpenseType GetDefaultExpenseType(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return ExpenseType.Unknown;

        var c = category.ToLower();

// NEED (fixed / essential)
        if (c.Contains("husleje") ||
            c.Contains("realkredit") ||
            c.Contains("rent") ||
            c.Contains("loan"))
            return ExpenseType.Need;

        if (c.Contains("dagligvarer") ||
            c.Contains("supermarket") ||
            c.Contains("food"))
            return ExpenseType.Need;

        if (c.Contains("forsikring") ||
            c.Contains("insurance"))
            return ExpenseType.Need;

// NICE (flexible)
        if (c.Contains("restaurant") ||
            c.Contains("takeaway") ||
            c.Contains("shopping") ||
            c.Contains("streaming"))
            return ExpenseType.Nice;

        return ExpenseType.Nice;

    }

}