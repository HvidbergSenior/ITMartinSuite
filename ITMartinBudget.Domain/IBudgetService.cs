using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Domain;

public interface IBudgetService
{
    IEnumerable<CategorySummary> GetSummary(List<BankTransaction> transactions, int year);
    YearSummary GetYearTotals(List<BankTransaction> transactions, int year);
}