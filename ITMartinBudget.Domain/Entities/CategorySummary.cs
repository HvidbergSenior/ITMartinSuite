using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class CategorySummary
{
    public Category Category { get; set; }

    public string DisplayName { get; set; } =
        string.Empty;

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    // positive income minus expenses
    public decimal Total => Income - Expenses;

    public int TransactionCount { get; set; }

    public TransactionFrequency Frequency { get; set; }

    public ExpenseType? ExpenseType { get; set; }
}