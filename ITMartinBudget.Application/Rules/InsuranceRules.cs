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
            Category.Forsikring),

        FixedExpense(
            "sygeforsikringen danmark",
            "Sygeforsikringen Danmark",
            Category.Forsikring),

        FixedExpense(
            "depotsikring",
            "Depotsikring",
            Category.Forsikring),

        FixedExpense(
            "tryg",
            "Tryg",
            Category.Forsikring,
            ComparingType.Word),

        FixedExpense(
            "topdanmark",
            "Topdanmark",
            Category.Forsikring),

        FixedExpense(
            "gjensidige",
            "Gjensidige",
            Category.Forsikring),

        FixedExpense(
            "codan",
            "Codan",
            Category.Forsikring,
            ComparingType.Word),

        FixedExpense(
            "if skadeforsikring",
            "If Forsikring",
            Category.Forsikring)
    ];
}