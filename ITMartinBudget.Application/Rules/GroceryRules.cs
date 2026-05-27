using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GroceryRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "netto",
            Title = "Netto",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rema",
            Title = "Rema 1000",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "foetex",
            Title = "Føtex",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "bilka",
            Title = "Bilka",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "lidl",
            Title = "Lidl",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kvickly",
            Title = "Kvickly",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "365discount",
            Title = "365discount",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "coop365",
            Title = "Coop 365",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dagli brugsen",
            Title = "Dagli'Brugsen",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "superbrugsen",
            Title = "SuperBrugsen",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "meny",
            Title = "Meny",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "spar",
            Title = "SPAR",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "min koebmand",
            Title = "Min Købmand",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reenberg groent",
            Title = "Reenberg Grønt",
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}