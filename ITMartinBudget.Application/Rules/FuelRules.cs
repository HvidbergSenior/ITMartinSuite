using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FuelRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "circle k",
            Title = "Circle K",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "uno x",
            Title = "Uno-X",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ingo",
            Title = "Ingo",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dk ok",
            Title = "OK",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vdk q8",
            Title = "Q8",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "shell",
            Title = "Shell",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "go on",
            Title = "Go'on",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "f24",
            Title = "F24",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "tankstation",
            Title = "Tankstation",
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}