using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class CategorySummary
{
    public Category Category { get; set; }
    public SubCategory SubCategory { get; set; }

    public decimal Income { get; set; }
    public decimal Expenses { get; set; }

    // 🔥 Derived
    public decimal Total => Income - Expenses;

    public string DisplayName { get; set; } = string.Empty;

    public int TransactionCount { get; set; }
    public TransactionFrequency Frequency { get; set; }

    // 🔥 ADD THIS
    public ExpenseType ExpenseType { get; set; }
}