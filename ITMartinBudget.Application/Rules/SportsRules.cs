using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class SportsRules
{
    public static readonly List<TransactionRule> Items =
    [
        FixedExpense(
            "hog fodbold",
            "HOG Fodbold",
            Category.Boern,
            ComparingType.Contains),

        FixedExpense(
            "hog hinnerup",
            "HOG Hinnerup",
            Category.Boern,
            ComparingType.Contains)
    ];
}