namespace ITMartinBudget.Domain.Entities;

public class YearSummary
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }

// 🔥 FIX
    public decimal Net => Income - Expenses;

}