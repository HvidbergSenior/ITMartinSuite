using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public sealed class FamilyPlanningViewModel
{
    public decimal CurrentMonthlyIncome { get; set; }

    public decimal FutureMonthlyIncome { get; set; }

    public decimal IncomeGap =>
        CurrentMonthlyIncome -
        FutureMonthlyIncome;

    public decimal PotentialSavings { get; set; }

    public IEnumerable<FamilyIncomeCategory>
        IncomeCategories { get; set; }
        = [];

    public IEnumerable<FamilyBudgetGroup>
        BudgetGroups { get; set; }
        = [];
    public List<BankTransaction>
        Transactions
    {
        get;
        set;
    }
}