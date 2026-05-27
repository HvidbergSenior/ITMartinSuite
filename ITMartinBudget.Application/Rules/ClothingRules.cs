using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ClothingRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "hm",
            Title = "H&M",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "about you",
            Title = "About You",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "only stores",
            Title = "Only",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "skechers",
            Title = "Skechers",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "stm sport",
            Title = "STM Sport",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "paw sko",
            Title = "Paw Sko",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "shopping4net",
            Title = "Shopping4Net",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ecco",
            Title = "Ecco",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "2nddeluxe",
            Title = "2ndDeluxe",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "blue tomato",
            Title = "Blue Tomato",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "klarna",
            Title = "Klarna",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "trendhim",
            Title = "Trendhim",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rivalxt",
            Title = "RivalXT",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reshopit",
            Title = "Reshopit",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "julie sandlau",
            Title = "Julie Sandlau",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sportmaster",
            Title = "Sportmaster",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "modekompagniet",
            Title = "Modekompagniet",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "mft knitwear",
            Title = "MFT Knitwear",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reshoppit",
            Title = "Reshoppit",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "bruuns galleri",
            Title = "Bruuns Galleri",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "hyldedeluxe",
            Title = "HyldeDeluxe",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ideal of sweden",
            Title = "Ideal of Sweden",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "zalando",
            Title = "Zalando",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vero moda",
            Title = "Vero Moda",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "jack and jones",
            Title = "Jack & Jones",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}