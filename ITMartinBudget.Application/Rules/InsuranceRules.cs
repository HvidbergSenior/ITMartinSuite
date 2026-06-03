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
            "Alka Forsikring",
            Category.Forsikring,
            ComparingType.Contains),

        Forsikring(
            "sygeforsikringen danmark",
            "Sygeforsikringen Danmark",
            Category.Forsikring,
            ComparingType.Contains),

        Forsikring(
            "depotsikring",
            "Depotsikring",
            Category.Forsikring,
            ComparingType.Contains),

        Forsikring(
            "tryg",
            "Tryg",
            Category.Forsikring,
            ComparingType.Word),

        Forsikring(
            "til alka",
            "Alka",
            Category.Forsikring,
            ComparingType.Contains),
    ];
}