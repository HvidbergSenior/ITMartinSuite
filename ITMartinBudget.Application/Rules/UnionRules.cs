using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class UnionRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "akademikernes a kasse",
            Title = "Akademikernes A-Kasse",
            Category = Category.FagforeningAKasse,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "socialpaedagogernes landsforbund",
            Title = "Socialpædagogernes Landsforbund",
            Category = Category.FagforeningAKasse,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "foa",
            Title = "FOA",
            Category = Category.FagforeningAKasse,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "3f",
            Title = "3F",
            Category = Category.FagforeningAKasse,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "hk",
            Title = "HK",
            Category = Category.FagforeningAKasse,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "dlf",
            Title = "Danmarks Lærerforening",
            Category = Category.FagforeningAKasse,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}