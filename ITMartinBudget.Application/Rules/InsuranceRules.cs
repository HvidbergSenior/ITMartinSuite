using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class InsuranceRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "alka forsikring",
            Title = "Alka Forsikring",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "sygeforsikringen danmark",
            Title = "Sygeforsikringen Danmark",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "depotsikring",
            Title = "Depotsikring",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "tryg",
            Title = "Tryg",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "topdanmark",
            Title = "Topdanmark",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "gjensidige",
            Title = "Gjensidige",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "codan",
            Title = "Codan",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "if skadeforsikring",
            Title = "If Forsikring",
            Category = Category.Forsikring,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}