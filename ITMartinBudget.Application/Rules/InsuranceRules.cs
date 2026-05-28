using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class InsuranceRules
{
    public static readonly List<TransactionRule> Items =
    [
        FixedExpense(
            "alka forsikring",
            "Alka Forsikring",
            Category.Forsikring,
            ComparingType.Contains),

        FixedExpense(
            "sygeforsikringen danmark",
            "Sygeforsikringen Danmark",
            Category.Forsikring,
            ComparingType.Contains),

        FixedExpense(
            "depotsikring",
            "Depotsikring",
            Category.Forsikring,
            ComparingType.Contains),

        FixedExpense(
            "tryg",
            "Tryg",
            Category.Forsikring,
            ComparingType.Word),

        FixedExpense(
            "topdanmark",
            "Topdanmark",
            Category.Forsikring,
            ComparingType.Contains),

        FixedExpense(
            "gjensidige",
            "Gjensidige",
            Category.Forsikring,
            ComparingType.Contains),

        FixedExpense(
            "codan",
            "Codan",
            Category.Forsikring,
            ComparingType.Word),

        FixedExpense(
            "if skadeforsikring",
            "If Forsikring",
            Category.Forsikring,
            ComparingType.Contains),
        FixedExpense(
            "til alka",
            "Alka",
            Category.Forsikring,
            ComparingType.Contains),
    ];
}