using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Models;

public sealed class FamilyPlanningViewModel
{
    public decimal CurrentIncome { get; set; }

    public decimal CurrentExpenses { get; set; }

    public decimal CurrentNetAmount { get; set; }

    public List<BudgetGroupSummary> BudgetGroups { get; set; } = [];
    public int MonthsLoaded { get; set; }

    public List<BankTransaction> Transactions { get; set; } = [];
    public decimal FixedIncome { get; set; }
}