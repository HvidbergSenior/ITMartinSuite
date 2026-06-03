using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GamingRules
{
    public static readonly List<TransactionRule> Items =
    [
        Entertainment(
            "playstation",
            "PlayStation",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "spilforsyningen",
            "Spilforsyningen",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "ubisoft",
            "Ubisoft",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "steam",
            "Steam",
            Category.Gaming,
            ComparingType.Word),

        Entertainment(
            "epic games",
            "Epic Games",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "nintendo",
            "Nintendo",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "xbox",
            "Xbox",
            Category.Gaming,
            ComparingType.Word),

        Entertainment(
            "riot games",
            "Riot Games",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "tsg platforms",
            "TSG Platforms",
            Category.Gaming,
            ComparingType.Contains),

        Entertainment(
            "apple com bill",
            "Apple Services",
            Category.Gaming,
            ComparingType.Contains),
        RulesFactory.Entertainment(
            "vdk sp royalcdkeys",
            "RoyalCDKeys",
            Category.Gaming,
            ComparingType.Exact),

        Entertainment(
            "vdk steamgames com 4259522985",
            "Steam",
            Category.Gaming,
            ComparingType.Exact),
    ];
}