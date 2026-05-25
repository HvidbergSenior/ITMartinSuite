using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TransportRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "circle k",
            Title = "Circle K",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "uno x",
            Title = "Uno-X",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ingo",
            Title = "Fuel",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "easypark",
            Title = "EasyPark",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "apcoa",
            Title = "Parking",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rejsekort",
            Title = "Rejsekort",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "brobizz",
            Title = "BroBizz",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "train",
            Title = "Train",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "den store cykelsmed",
            Title = "Bike",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },
    ];
}
