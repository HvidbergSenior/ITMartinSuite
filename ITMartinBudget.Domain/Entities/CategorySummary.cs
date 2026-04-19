using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class CategorySummary
{
    public MainCategory MainCategory { get; set; }
    public SubCategory SubCategory { get; set; }

    public decimal Total { get; set; }
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }

    public int TransactionCount { get; set; }

    public TransactionFrequency Frequency { get; set; } = TransactionFrequency.Unknown;
    public ExpenseType ExpenseType { get; set; } = ExpenseType.Unknown;
}