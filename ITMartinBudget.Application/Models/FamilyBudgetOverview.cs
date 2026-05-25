namespace ITMartinBudget.Application.Models;

public class FamilyBudgetOverview
{
    public string Title { get; set; } = "";

    public decimal MonthlyIncome { get; set; }

    public decimal MonthlyFixedExpenses { get; set; }

    public decimal MonthlyVariableExpenses { get; set; }

    public decimal MonthlyRemaining =>
        MonthlyIncome
        - MonthlyFixedExpenses
        - MonthlyVariableExpenses;

    public decimal ExpectedRemainingPeriod { get; set; }

    public int MonthsRemaining { get; set; }
}