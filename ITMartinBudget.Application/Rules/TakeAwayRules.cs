using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TakeAwayRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "wolt",
            Title = "Wolt",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "justeat",
            Title = "Just Eat",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "mcdonald",
            Title = "McDonalds",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "burger king",
            Title = "Burger King",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sunset",
            Title = "Sunset Boulevard",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kfc",
            Title = "KFC",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dominos",
            Title = "Domino's",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "pizza",
            Title = "Pizza",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sushi",
            Title = "Sushi",
            Category = Category.Takeaway,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}