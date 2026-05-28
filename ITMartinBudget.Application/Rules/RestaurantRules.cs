using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class RestaurantRules
{
    public static readonly List<TransactionRule> Items =
    [
        RestaurantCafe(
            "hanzo",
            "Hanzo",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "umashi",
            "Umashi",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "thaiplus",
            "ThaiPlus",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "taxas",
            "Taxas",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "aeblehaven",
            "Æblehaven",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "kasses skovpoelser",
            "Kasses Skovpølser",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "rasses skovpoelser",
            "Rasses Skovpølser",
            Category.Restaurant,
            ComparingType.Contains)
    ];
}