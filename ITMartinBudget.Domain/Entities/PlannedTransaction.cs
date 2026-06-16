using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class PlannedTransaction
{
    public int Id { get; set; }

    public DateTime ExpectedDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public TransactionType TransactionType { get; set; }

    public Category Category { get; set; }

    public BudgetGroup BudgetGroup { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal RecurringIntervalMonths { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
