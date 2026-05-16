using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public string Description { get; set; } =
        string.Empty;

    public string NormalizedDescription { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    public TransactionType TransactionType { get; set; }

    public Category Category { get; set; }

    public BudgetGroup BudgetGroup { get; set; }

    public string Title { get; set; } =
        string.Empty;

    public bool IsRecurring { get; set; }

    public DateTime ImportedAt { get; set; } =
        DateTime.UtcNow;
}