using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HobbyRules
{
    public static readonly List<TransactionRule> Items =
    [
        EntertainmentExpense(
            "danguitar",
            "DanGuitar",
            Category.Fritid),

        EntertainmentExpense(
            "rito",
            "Rito",
            Category.Fritid,
            ComparingType.Word),

        EntertainmentExpense(
            "kreativ kerami",
            "Kreativ Keramik",
            Category.Fritid),

        EntertainmentExpense(
            "saxo",
            "Saxo",
            Category.Fritid,
            ComparingType.Word),

        EntertainmentExpense(
            "holdet",
            "Holdet",
            Category.Fritid,
            ComparingType.Word)
    ];
}