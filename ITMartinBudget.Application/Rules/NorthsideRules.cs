using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class NorthsideRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.Northside(
            "dtd group",
            "NorthSide"),

        RulesFactory.Northside(
            "northside",
            "NorthSide"),

        RulesFactory.Northside(
            "ticketmaster northside",
            "NorthSide Ticket"),

        RulesFactory.Northside(
            "north side",
            "NorthSide")
    ];
}