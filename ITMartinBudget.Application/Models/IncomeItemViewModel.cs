namespace ITMartinBudget.Application.Models;

public sealed class IncomeItemViewModel
{
    public string Title { get; set; } = string.Empty;

    public decimal ExpectedAmount { get; set; }
}