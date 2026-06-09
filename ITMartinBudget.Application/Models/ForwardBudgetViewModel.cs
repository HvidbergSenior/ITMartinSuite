namespace ITMartinBudget.Application.Models;

public sealed class ForwardBudgetViewModel
{
    public decimal ExpectedMonthlyIncome { get; set; }

    public decimal FixedMonthlyExpenses { get; set; }

    public decimal AdjustableMonthlyExpenses { get; set; }

    public decimal FreeDisposableAmount =>
        ExpectedMonthlyIncome -
        FixedMonthlyExpenses -
        AdjustableMonthlyExpenses;
    
    public List<IncomeItemViewModel> IncomeItems { get; set; } = [];

    public List<FixedExpenseViewModel> FixedExpenses { get; set; } = [];

    public List<FixedExpenseViewModel> RecurringAdjustableExpenses { get; set; } = [];

    public List<AdjustableBudgetGroupViewModel> AdjustableGroups { get; set; } = [];
    public List<AdjustableBudgetGroupViewModel>
        SemiAdjustableGroups
    {
        get;
        set;
    } = [];
    public List<AdjustableBudgetGroupViewModel>
        IgnoredGroups
    {
        get;
        set;
    } = [];
}