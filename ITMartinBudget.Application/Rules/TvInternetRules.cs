using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TvInternetRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "allente",
            Title = "Allente",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "telenor",
            Title = "Telenor",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "yousee",
            Title = "YouSee",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "norlys",
            Title = "Norlys",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "waoo",
            Title = "Waoo",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "hiper",
            Title = "Hiper",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "eesy",
            Title = "eesy",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "oister",
            Title = "Oister",
            Category = Category.TelefonTvInternet,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}