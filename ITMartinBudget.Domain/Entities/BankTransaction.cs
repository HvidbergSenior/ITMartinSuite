using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public string Description { get; set; } = "";

    public decimal Amount { get; set; }

    public Category Category { get; set; }

    public TransactionType TransactionType { get; set; }

    public ExpenseType? ExpenseType { get; set; }
}