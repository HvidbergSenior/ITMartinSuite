using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class SportsRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "hog hinnerup",
            Title = "HOG Hinnerup",
            Category = Category.Boern,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}