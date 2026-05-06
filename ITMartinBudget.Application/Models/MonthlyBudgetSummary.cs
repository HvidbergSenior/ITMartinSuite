namespace ITMartinBudget.Application.Models;

public class MonthlyBudgetSummary
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthName { get; set; } = "";

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public decimal Net => Income - Expenses;
}