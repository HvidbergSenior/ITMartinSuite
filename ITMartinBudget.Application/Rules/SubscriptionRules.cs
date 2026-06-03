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
            "youtube premium",
            "YouTube Premium",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "hbo",
            "HBO",
            Category.Subscription,
            ComparingType.Word),

        Subscription(
            "disney",
            "Disney+",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "viaplay",
            "Viaplay",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "tv2 play",
            "TV2 Play",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "allente",
            "Allente",
            Category.Subscription,
            ComparingType.Contains),

        WorkExpense(
            "chatgpt",
            "ChatGPT",
            Category.Subscription,
            ComparingType.Contains),

        WorkExpense(
            "openai",
            "OpenAI",
            Category.Subscription,
            ComparingType.Contains),

        PaymentForChildren(
            "fitnessunited",
            "Fitness United",
            Category.Subscription,
            ComparingType.Contains),
        
        Subscription(
            "suno inc",
            "Suno",
            Category.Subscription,
            ComparingType.Contains),
        
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
        RulesFactory.Subscription(
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
    ];
}