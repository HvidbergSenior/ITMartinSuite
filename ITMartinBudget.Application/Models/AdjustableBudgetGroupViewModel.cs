using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public sealed class AdjustableBudgetGroupViewModel
{
    public BudgetGroup BudgetGroup { get; set; }

    public decimal LastMonthAmount { get; set; }

    public decimal Last3MonthsAverage { get; set; }

    public decimal Last12MonthsAverage { get; set; }

    public decimal CurrentYearTotal { get; set; }

    public int TransactionCount { get; set; }

    public decimal SuggestedReduction =>
        Math.Max(
            0,
            Last3MonthsAverage -
            Last12MonthsAverage);

    public List<TransactionSummaryViewModel>
        RecentTransactions
    {
        get;
        set;
    } = [];
}