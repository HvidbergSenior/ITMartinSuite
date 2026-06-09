namespace ITMartinBudget.Application.Models;

public sealed class FixedExpenseViewModel
{
    public string Title { get; set; } = string.Empty;

    public decimal MonthlyAmount { get; set; }

    public decimal RecurringIntervalMonths { get; set; }
}