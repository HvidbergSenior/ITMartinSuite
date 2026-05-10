using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    // RAW BANK TEXT
    public string Description { get; set; } =
        string.Empty;

    // NORMALIZED FOR MATCHING
    public string NormalizedDescription { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    public Category Category { get; set; }

    // PURE MONEY DIRECTION
    public TransactionType TransactionType { get; set; }

    public ExpenseType? ExpenseType { get; set; }

    // WHICH RULE MATCHED
    public int? MatchedRuleId { get; set; }

    public CategoryRule? MatchedRule { get; set; }

    // REVIEW SYSTEM
    public bool NeedsReview { get; set; }

    public bool IsCategorizedManually { get; set; }

    public DateTime ImportedAt { get; set; } =
        DateTime.UtcNow;
}