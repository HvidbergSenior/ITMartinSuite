using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ParkingRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "easypark",
            Title = "EasyPark",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "apcoa",
            Title = "APCOA",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "parkzone",
            Title = "ParkZone",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "q park",
            Title = "Q-Park",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "city p hus",
            Title = "City P-Hus",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "parkeringskompagniet",
            Title = "Parkeringskompagniet",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "parkman",
            Title = "ParkMan",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "onepark",
            Title = "OnePark",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "parkering aarhus",
            Title = "Aarhus Parkering",
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}