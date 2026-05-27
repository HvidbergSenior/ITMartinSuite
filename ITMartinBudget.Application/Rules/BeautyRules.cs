using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class BeautyRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "piet",
            Title = "Frisør Piet",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "lyko",
            Title = "Lyko",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "matas",
            Title = "Matas",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "normal",
            Title = "Normal",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "magasin",
            Title = "Magasin Beauty",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sephora",
            Title = "Sephora",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "nicehair",
            Title = "NiceHair",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "frisoer",
            Title = "Frisør",
            Category = Category.Sundhed,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}