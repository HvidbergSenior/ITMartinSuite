using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }

    public string Category { get; set; } = default!;
    public string MainCategory { get; set; } = default!;
    public ExpenseType ExpenseType { get; set; } = ExpenseType.Unknown;
}