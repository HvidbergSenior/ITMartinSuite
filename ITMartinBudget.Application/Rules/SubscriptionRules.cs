using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class SubscriptionRules
{
    public static readonly List<TransactionRule> Items =
    [
        FixedExpense(
            "netflix",
            "Netflix",
            Category.Subscription),

        FixedExpense(
            "spotify",
            "Spotify",
            Category.Subscription),

        FixedExpense(
            "google one",
            "Google One",
            Category.Subscription),

        FixedExpense(
            "youtube premium",
            "YouTube Premium",
            Category.Subscription),

        FixedExpense(
            "hbo",
            "HBO",
            Category.Subscription,
            ComparingType.Word),

        FixedExpense(
            "disney",
            "Disney+",
            Category.Subscription),

        FixedExpense(
            "viaplay",
            "Viaplay",
            Category.Subscription),

        FixedExpense(
            "tv2 play",
            "TV2 Play",
            Category.Subscription),

        FixedExpense(
            "allente",
            "Allente",
            Category.Subscription),

        FixedExpense(
            "chatgpt",
            "ChatGPT",
            Category.Subscription),

        FixedExpense(
            "openai",
            "OpenAI",
            Category.Subscription),

        FixedExpense(
            "dropbox",
            "Dropbox",
            Category.Subscription),

        FixedExpense(
            "microsoft",
            "Microsoft",
            Category.Subscription),

        FixedExpense(
            "adobe",
            "Adobe",
            Category.Subscription),

      
    ];
}