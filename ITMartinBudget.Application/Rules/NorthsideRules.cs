using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class NorthsideRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.Entertainment(
            "dtd group",
            "NorthSide",
            Category.Northside,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "northside",
            "NorthSide",
            Category.Northside,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "ticketmaster northside",
            "NorthSide",
            Category.Northside,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "north side",
            "NorthSide",
            Category.Northside,
            ComparingType.Contains)
    ];
}