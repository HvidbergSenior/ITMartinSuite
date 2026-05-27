using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class LeisureRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "universal music",
            Title = "Universal Music",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "danguitar",
            Title = "DanGuitar",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rito",
            Title = "Rito",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kreativ kerami",
            Title = "Kreativ Keramik",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "fof aarhus",
            Title = "FOF Aarhus",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "klitmoeller",
            Title = "Klitmøller",
            Category = Category.Rejse,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "radisson blu",
            Title = "Radisson Blu",
            Category = Category.Rejse,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // Home / Lifestyle

        new()
        {
            Pattern = "kop og kande",
            Title = "Kop & Kande",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "malerfirma tidens farver",
            Title = "Tidens Farver",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "skumhuset",
            Title = "Skumhuset",
            Category = Category.Fritid,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "chokolet",
            Title = "Chokolade",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },
        new()
        {
            Pattern = "noeddebutikken",
            Title = "Nøddebutikken",
            Category = Category.Cafe,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "roede kors butik",
            Title = "Røde Kors Butik",
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vesterlund efterskol",
            Title = "Vesterlund Efterskole",
            Category = Category.Boern,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}