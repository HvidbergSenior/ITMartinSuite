using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class NorthsideRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "dtd group",
            Title = "NorthSide",
            Category = Category.Northside,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "northside",
            Title = "NorthSide",
            Category = Category.Northside,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ticketmaster northside",
            Title = "NorthSide Ticket",
            Category = Category.Northside,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "north side",
            Title = "NorthSide",
            Category = Category.Northside,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}