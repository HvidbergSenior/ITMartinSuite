using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GamingRules
{
    public static readonly List<TransactionRule> Items =
    [
        EntertainmentExpense(
            "playstation",
            "PlayStation"),

        EntertainmentExpense(
            "spilforsyningen",
            "Spilforsyningen"),

        EntertainmentExpense(
            "ubisoft",
            "Ubisoft"),

        EntertainmentExpense(
            "steam",
            "Steam"),

        EntertainmentExpense(
            "epic games",
            "Epic Games"),

        EntertainmentExpense(
            "nintendo",
            "Nintendo"),

        EntertainmentExpense(
            "xbox",
            "Xbox"),

        EntertainmentExpense(
            "riot games",
            "Riot Games"),

        EntertainmentExpense(
            "tsg platforms",
            "TSG Platforms"),

        EntertainmentExpense(
            "apple com bill",
            "Apple Services")
    ];
}