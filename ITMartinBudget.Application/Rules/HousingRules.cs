using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HousingRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Mortgage / Housing

        new()
        {
            Pattern = "termin jyske realkredit",
            Title = "Jyske Realkredit",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "aarhus kommune ejendomsskat",
            Title = "Ejendomsskat",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "parcelforeningen",
            Title = "Parcelforening",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "skattestyrelsen",
            Title = "Skattestyrelsen",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // Utilities

        new()
        {
            Pattern = "aarhus vand",
            Title = "Aarhus Vand",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "kredsloeb",
            Title = "Kredsløb",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "norlys energi",
            Title = "Norlys Energi",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "ewii",
            Title = "EWII",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "andel energi",
            Title = "Andel Energi",
            Category = Category.Bolig,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

       
    ];
}