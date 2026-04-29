using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Category Category { get; set; }
    public SubCategory SubCategory { get; set; }

    public ExpenseType ExpenseType { get; set; } = ExpenseType.Optional;
    public TransactionType Type => Amount >= 0 
        ? TransactionType.Income 
        : TransactionType.Expense;

    // 🔥 NEW
    public string? MobilePayName { get; set; }
    public Guid? ContactId { get; set; }
}