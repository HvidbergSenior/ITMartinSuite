using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public sealed class ForecastViewModel
{
    public List<MonthlySnapshot> History { get; set; } = [];
    public List<MonthlySnapshot> Projected { get; set; } = [];
    public decimal AvgIncome { get; set; }
    public decimal AvgExpenses { get; set; }
    public decimal IncomeSlope { get; set; }
    public decimal ExpenseSlope { get; set; }
    public List<CuttableExpenseItem> CuttableExpenses { get; set; } = [];
    public List<MonthlySnapshot> AllPoints => [..History, ..Projected];

    public decimal ProjectedNextMonthNet =>
        Projected.FirstOrDefault()?.Net ?? (AvgIncome - AvgExpenses);
}

public sealed class MonthlySnapshot
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net => Income - Expenses;
    public bool IsProjected { get; set; }
    public string Label => $"{new DateTime(Year, Month, 1):MMM yy}";
}

public sealed class CuttableExpenseItem
{
    public BudgetGroup Group { get; set; }
    public string DisplayName { get; set; } = "";
    public decimal MonthlyAverage { get; set; }
    public decimal RealisticSaving => Math.Round(MonthlyAverage * 0.20m);
    public int TransactionCount { get; set; }
    public BudgetGroupType GroupType { get; set; }
}
