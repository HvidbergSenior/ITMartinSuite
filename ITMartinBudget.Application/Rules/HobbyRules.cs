using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HobbyRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Hobby

        new()
        {
            Pattern = "danguitar",
            Title = "DanGuitar",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rito",
            Title = "Rito",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "joytunes",
            Title = "JoyTunes",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "fof aarhus",
            Title = "FOF Aarhus",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kreativ kerami",
            Title = "Kreativ Keramik",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "saxo",
            Title = "Saxo",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sportmaster",
            Title = "Sportmaster",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "stm sport",
            Title = "STM Sport",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "holdet",
            Title = "Holdet",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}