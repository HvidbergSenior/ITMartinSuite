using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BudgetGroupSummary
{
    public BudgetGroup BudgetGroup { get; set; }

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public decimal Total =>
        Income - Expenses;

    public int TransactionCount { get; set; }
}