namespace ITMartinBudget.Application.Extensions;

public static class BudgetExtensions
{
    public static string ToKr(
        this decimal value)
    {
        return
            value.ToString("N2")
            + " kr.";
    }
}