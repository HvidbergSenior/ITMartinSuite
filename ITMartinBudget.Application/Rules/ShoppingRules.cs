using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ShoppingRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "salling",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "matas",
            Title = "Health & Beauty",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "hm",
            Title = "Clothing",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "about you",
            Title = "Clothing",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "blue tomato",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ecco",
            Title = "Shoes",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "proshop",
            Title = "Electronics",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "danguitar",
            Title = "Music Equipment",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "elgiganten",
            Title = "Electronics",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vvs eksperten",
            Title = "Home Improvement",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ikea",
            Title = "Home",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "silvan",
            Title = "Home Improvement",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kjaer og sommerfeldt",
            Title = "Wine",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}