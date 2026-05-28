using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HobbyRules
{
    public static readonly List<TransactionRule> Items =
    [
        Entertainment(
            "danguitar",
            "DanGuitar",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "rito",
            "Rito",
            Category.Fritid,
            ComparingType.Word),

        Entertainment(
            "kreativ kerami",
            "Kreativ Keramik",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "saxo",
            "Saxo",
            Category.Fritid,
            ComparingType.Word),

        Entertainment(
            "holdet",
            "Holdet",
            Category.Fritid,
            ComparingType.Word)
    ];
}