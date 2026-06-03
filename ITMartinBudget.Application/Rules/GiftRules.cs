using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GiftRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.Subscription(
            "hjemmet",
            "Hjemmet",
            Category.Gaver,
            ComparingType.Contains),
        RulesFactory.GiftFromUs(
            "boernecancerfonden",
            "Børnecancerfonden",
            ComparingType.Contains),

        RulesFactory.GiftFromUs(
            "bla kors",
            "Blå Kors",
            ComparingType.Contains),
    ];
}