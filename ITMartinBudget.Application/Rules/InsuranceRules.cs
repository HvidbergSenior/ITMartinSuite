using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class InsuranceRules
{
    public static readonly List<TransactionRule> Items =
    [
        Forsikring(
            "alka forsikring",
            "Alka",
            Category.Forsikring,
            ComparingType.Contains,
            1
            ),

        Forsikring(
            "sygeforsikringen danmark",
            "Sygeforsikringen Danmark",
            Category.Forsikring,
            ComparingType.Contains,
            3),

        Forsikring(
            "depotsikring",
            "Depotsikring",
            Category.Forsikring,
            ComparingType.Contains, 1),

        Forsikring(
            "tryg",
            "Tryg",
            Category.Forsikring,
            ComparingType.Word,1),

        Forsikring(
            "til alka",
            "Alka",
            Category.Forsikring,
            ComparingType.Contains),
    ];
}