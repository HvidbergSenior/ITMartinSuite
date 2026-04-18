
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class BudgetService
{
    public IEnumerable<object> GetSummary(IEnumerable<Transaction> data, int year)
    {
        return data
            .Where(x => x.Date.Year == year)
            .GroupBy(x => x.Category)
            .Select(g => new
            {
                Category = g.Key,
                Total = g.Sum(x => x.Amount)
            });
    }
}