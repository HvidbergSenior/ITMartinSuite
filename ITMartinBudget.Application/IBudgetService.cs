using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application;

public interface IBudgetService
{
    IEnumerable<CategorySummary> GetSummary(List<BankTransaction> transactions, int year);
    YearSummary GetYearTotals(List<BankTransaction> transactions, int year);
    IEnumerable<BudgetOverviewItem> GetBudgetOverview(List<BankTransaction> transactions, int year);
}
