using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GiftRules
{
    public static readonly List<TransactionRule> Items =
    [
       
        RulesFactory.GiftFromUs(
            "hjemmet",
            "Hjemmet",
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