using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class CafeRules
{
    public static readonly List<TransactionRule> Items =
    [
        RestaurantCafe(
            "starbucks",
            "Starbucks",
            Category.Cafe),

        RestaurantCafe(
            "lagkagehuset",
            "Lagkagehuset",
            Category.Cafe),

        RestaurantCafe(
            "ibens kaffehus",
            "Ibens Kaffehus",
            Category.Cafe),

        RestaurantCafe(
            "casino bar",
            "Casino Bar",
            Category.Cafe),

        RestaurantCafe(
            "ejvinds stormgade",
            "Ejvinds Stormgade",
            Category.Cafe),

        RestaurantCafe(
            "sams pita",
            "Sams Pita",
            Category.Cafe),

        RestaurantCafe(
            "espresso house",
            "Espresso House",
            Category.Cafe),

        RestaurantCafe(
            "joe and the juice",
            "Joe & The Juice",
            Category.Cafe),

        RestaurantCafe(
            "emmerys",
            "Emmerys",
            Category.Cafe),

        RestaurantCafe(
            "baresso",
            "Baresso",
            Category.Cafe),

        RestaurantCafe(
            "peter gift",
            "Gift",
            Category.Cafe)
    ];
}