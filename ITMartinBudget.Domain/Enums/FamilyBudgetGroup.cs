using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Domain.Enums;

public sealed class FamilyBudgetGroup
{
    public string Name { get; set; } = "";

    public decimal MonthlyAmount { get; set; }

    public decimal PercentOfIncome { get; set; }

    public decimal PotentialReduction { get; set; }

    public BudgetGroupPriority Priority
    {
        get;
        set;
    }

    public List<BankTransaction> Transactions
    {
        get;
        set;
    } = [];
}