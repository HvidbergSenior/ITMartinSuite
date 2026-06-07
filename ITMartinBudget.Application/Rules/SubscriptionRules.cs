using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class SubscriptionRules
{
    public static readonly List<TransactionRule> Items =
    [
        Subscription(
            "netflix",
            "Netflix",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "spotify",
            "Spotify",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "google one",
            "Google One",
            Category.Subscription,
            ComparingType.Contains),
        
        Subscription(
            "viaplay",
            "Viaplay",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "chatgpt",
            "ChatGPT",
            Category.Subscription,
            ComparingType.Contains, 1),

        Subscription(
            "openai",
            "OpenAI",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "fitnessunited",
            "Fitness United",
            Category.Subscription,
            ComparingType.Contains),
        
        Subscription(
            "suno inc",
            "Suno",
            Category.Subscription,
            ComparingType.Contains, 12),
        
        Subscription(
            "joytunes",
            "JoyTunes",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "google play",
            "Google Play",
            Category.Subscription,
            ComparingType.Contains),
        
        RulesFactory.GeneralShopping(
            "dk story house egmont a s",
            "Story House Egmont",
            Category.Fritid,
            ComparingType.Exact),

        RulesFactory.Subscription(
            "mobilepay bedre psykiatri",
            "Donation",
            Category.Subscription,
            ComparingType.Exact),

        RulesFactory.Subscription(
            "teleno32107104134621",
            "Telenor",
            Category.TelefonTvInternet,
            ComparingType.Exact),
        RulesFactory.Subscription(
            "vdk jetbrains",
            "JetBrains",
            Category.Subscription,
            ComparingType.Exact, 12),

        RulesFactory.Subscription(
            "vdk one com",
            "One.com",
            Category.TelefonTvInternet,
            ComparingType.Exact, 12),
    ];
}