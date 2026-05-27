using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "ikea",
            Title = "IKEA",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ilva",
            Title = "ILVA",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "silvan",
            Title = "Silvan",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vvs eksperten",
            Title = "VVS Eksperten",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kop og kande",
            Title = "Kop & Kande",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "jem og fix",
            Title = "Jem & Fix",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "bauhaus",
            Title = "Bauhaus",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "harald nyborg",
            Title = "Harald Nyborg",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "biltema",
            Title = "Biltema",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "plantorama",
            Title = "Plantorama",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "jysk",
            Title = "JYSK",
            Category = Category.Hjem,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}