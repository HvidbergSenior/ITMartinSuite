using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class SportsRules
{
    public static readonly List<TransactionRule> Items =
    [
        PaymentForChildren(
            "hog fodbold",
            "HOG Fodbold",
            Category.Subscription,
            ComparingType.Contains),

        PaymentForChildren(
            "hog hinnerup",
            "HOG Hinnerup",
            Category.Subscription,
            ComparingType.Contains)
    ];
}