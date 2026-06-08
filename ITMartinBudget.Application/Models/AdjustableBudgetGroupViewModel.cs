using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public sealed class AdjustableBudgetGroupViewModel
{
    public BudgetGroup BudgetGroup { get; set; }

    public decimal MonthlyAverage { get; set; }

    public decimal CurrentYearTotal { get; set; }

    public int TransactionCount { get; set; }
    
    public List<TransactionSummaryViewModel>
        RecentTransactions
    {
        get;
        set;
    } = [];
}