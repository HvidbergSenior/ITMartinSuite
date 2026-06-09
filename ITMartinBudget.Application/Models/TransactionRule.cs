using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public class TransactionRule
{
    public string Pattern { get; set; } =
        string.Empty;

    public string Title { get; set; } =
        string.Empty;

    public Category Category { get; set; }

    public BudgetGroup BudgetGroup { get; set; }

    public decimal RecurringIntervalMonths { get; set; }
    public TransactionType TransactionType { get; set; }
    public int Priority { get; init; }
    public ComparingType ComparingType { get; set; }
        = ComparingType.Contains;
    public List<string> Examples { get; set; } = [];
}