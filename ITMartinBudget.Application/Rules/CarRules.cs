using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class CarRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Repair / Maintenance

        new()
        {
            Pattern = "thansen",
            Title = "thansen",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sejr jensen auto",
            Title = "Sejr Jensen Auto",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "quickpot",
            Title = "QuickPot",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "euromaster",
            Title = "Euromaster",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "autobutler",
            Title = "AutoButler",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "skorstensgaard",
            Title = "Skorstensgaard",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // Car Wash

        new()
        {
            Pattern = "wash world",
            Title = "Wash World",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // Vehicle Tax

        new()
        {
            Pattern = "sktst motor",
            Title = "Motorafgift",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "dmr",
            Title = "Motorregister",
            Category = Category.BilVedligehold,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}