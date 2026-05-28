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
            Category.Restaurant),

        RestaurantCafe(
            "umashi",
            "Umashi",
            Category.Restaurant),

        RestaurantCafe(
            "thaiplus",
            "ThaiPlus",
            Category.Restaurant),

        RestaurantCafe(
            "taxas",
            "Taxas",
            Category.Restaurant),

        RestaurantCafe(
            "aeblehaven",
            "Æblehaven",
            Category.Restaurant),

        RestaurantCafe(
            "kasses skovpoelser",
            "Kasses Skovpølser",
            Category.Restaurant),

        RestaurantCafe(
            "rasses skovpoelser",
            "Rasses Skovpølser",
            Category.Restaurant)
    ];
}