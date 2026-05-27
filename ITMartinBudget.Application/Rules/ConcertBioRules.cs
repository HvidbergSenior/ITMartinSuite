using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ConcertBioRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Cinema

        new()
        {
            Pattern = "cinemaxx",
            Title = "CinemaxX",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "paradisbio",
            Title = "Paradis Bio",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "oest for paradis",
            Title = "Øst for Paradis",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // Concerts / Tickets

        new()
        {
            Pattern = "ticketmaster",
            Title = "Ticketmaster",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "musikhuset",
            Title = "Musikhuset",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "train",
            Title = "Train",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "voxhall",
            Title = "VoxHall",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "tivoli friheden",
            Title = "Tivoli Friheden",
            Category = Category.Koncert,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}