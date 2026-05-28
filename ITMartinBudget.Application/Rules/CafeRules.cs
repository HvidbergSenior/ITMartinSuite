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
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "lagkagehuset",
            "Lagkagehuset",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "ibens kaffehus",
            "Ibens Kaffehus",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "casino bar",
            "Casino Bar",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "ejvinds stormgade",
            "Ejvinds",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "sams pita",
            "Sams Pita",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "espresso house",
            "Espresso House",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "joe and the juice",
            "Joe & The Juice",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "emmerys",
            "Emmerys",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "baresso",
            "Baresso",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "peter gift",
            "Peter Gift",
            Category.Cafe,
            ComparingType.Contains),

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
            "taxas",
            "Taxas",
            Category.Restaurant,
            ComparingType.Contains)
    ];
}