using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class PublicTransportRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "rejsekort",
            Title = "Rejsekort",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "brobizz",
            Title = "BroBizz",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dsb",
            Title = "DSB",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "midttrafik",
            Title = "Midttrafik",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "letbane",
            Title = "Letbane",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "molslinjen",
            Title = "Molslinjen",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "go collect",
            Title = "GoCollective",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "flixbus",
            Title = "FlixBus",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kombardo",
            Title = "Kombardo",
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}