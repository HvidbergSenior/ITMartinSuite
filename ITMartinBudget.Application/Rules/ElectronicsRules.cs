using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ElectronicsRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "proshop",
            Title = "Proshop",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "elgiganten",
            Title = "Elgiganten",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "power",
            Title = "Power",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "apple",
            Title = "Apple",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "humac",
            Title = "Humac",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "fonik",
            Title = "Fonik",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "av cable",
            Title = "AV-Cables",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "computersalg",
            Title = "ComputerSalg",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "komplett",
            Title = "Komplett",
            Category = Category.Elektronik,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}