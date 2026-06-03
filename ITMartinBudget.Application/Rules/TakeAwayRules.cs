using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TakeAwayRules
{
    public static readonly List<TransactionRule> Items =
    [
        RestaurantCafe(
            "wolt",
            "Wolt",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "justeat",
            "Just Eat",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "mcdonald",
            "McDonalds",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "burger king",
            "Burger King",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "sunset",
            "Sunset Boulevard",
            Category.Takeaway,
            ComparingType.Word),

        RestaurantCafe(
            "kfc",
            "KFC",
            Category.Takeaway,
            ComparingType.Word),

        RestaurantCafe(
            "pizza",
            "Pizza",
            Category.Takeaway,
            ComparingType.Word),

        RestaurantCafe(
            "sushi",
            "Sushi",
            Category.Takeaway,
            ComparingType.Word),
        
        RestaurantCafe(
            "mackies",
            "Mackies",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "wok shop",
            "Wok Shop",
            Category.Takeaway,
            ComparingType.Contains),

    ];
}