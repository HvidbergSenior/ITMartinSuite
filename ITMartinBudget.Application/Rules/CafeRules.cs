using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class CafeRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "starbucks",
            Title = "Starbucks",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "lagkagehuset",
            Title = "Lagkagehuset",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ibens kaffehus",
            Title = "Ibens Kaffehus",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "casino bar",
            Title = "Casino Bar",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ejvinds stormgade",
            Title = "Ejvinds Stormgade",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "butler loftet",
            Title = "Butler Loftet",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sams pita",
            Title = "Sams Pita",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "espresso house",
            Title = "Espresso House",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "joe and the juice",
            Title = "Joe & The Juice",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "emmerys",
            Title = "Emmerys",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "baresso",
            Title = "Baresso",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}