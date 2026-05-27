using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GamingRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "playstation",
            Title = "PlayStation",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "spilforsyningen",
            Title = "Spilforsyningen",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ubisoft",
            Title = "Ubisoft",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "steam",
            Title = "Steam",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "epic games",
            Title = "Epic Games",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "nintendo",
            Title = "Nintendo",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "xbox",
            Title = "Xbox",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "riot games",
            Title = "Riot Games",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "tsg platforms",
            Title = "TSG Platforms",
            Category = Category.Gaming,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}