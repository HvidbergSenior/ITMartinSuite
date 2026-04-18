using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class BudgetService
{
    public IEnumerable<CategorySummary> GetSummary(IEnumerable<BankTransaction> data, int year)
    {
        return data
            .Where(x => x.Date.Year == year)
            .GroupBy(x => x.Category)
            .Select(g => new CategorySummary
            {
                Category = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => Math.Abs(x.Total));
    }

    public YearSummary GetYearTotals(IEnumerable<BankTransaction> data, int year)
    {
        var filtered = data.Where(x => x.Date.Year == year);

        return new YearSummary
        {
            Income = filtered.Where(x => x.Amount > 0).Sum(x => x.Amount),
            Expenses = filtered.Where(x => x.Amount < 0).Sum(x => x.Amount)
        };
    }
}