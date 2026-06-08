using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TvInternetRules
{
    public static readonly List<TransactionRule> Items =
    [
        Subscription(
            "telenor",
            "Telenor",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "yousee",
            "YouSee",
            Category.Subscription,
            ComparingType.Contains),
        
    ];
}