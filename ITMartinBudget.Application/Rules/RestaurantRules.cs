using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class RestaurantRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "hanzo",
            Title = "Hanzo",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "umashi",
            Title = "Umashi",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "butler loftet",
            Title = "Butler Loftet",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "thaiplus",
            Title = "ThaiPlus",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "taxas",
            Title = "Taxas",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sams pita",
            Title = "Sams Pita",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "aeblehaven",
            Title = "Æblehaven",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "casino bar",
            Title = "Casino Bar",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ejvinds stormgade",
            Title = "Ejvinds",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kasses skovpoelser",
            Title = "Kasses Skovpølser",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rasses skovpoelser",
            Title = "Rasses Skovpølser",
            Category = Category.Restaurant,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}