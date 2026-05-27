using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class SubscriptionRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "netflix",
            Title = "Netflix",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "spotify",
            Title = "Spotify",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "google one",
            Title = "Google One",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "apple com bill",
            Title = "Apple Services",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "youtube premium",
            Title = "YouTube Premium",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "hbo",
            Title = "HBO",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "disney",
            Title = "Disney+",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "viaplay",
            Title = "Viaplay",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "tv2",
            Title = "TV2 Play",
            Category = Category.Streaming,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

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
            Pattern = "chatgpt",
            Title = "ChatGPT",
            Category = Category.Apps,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "openai",
            Title = "OpenAI",
            Category = Category.Apps,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "dropbox",
            Title = "Dropbox",
            Category = Category.Apps,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "microsoft",
            Title = "Microsoft",
            Category = Category.Apps,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "adobe",
            Title = "Adobe",
            Category = Category.Apps,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        }
    ];
}