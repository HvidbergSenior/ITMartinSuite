using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TvInternetRules
{
    public static readonly List<TransactionRule> Items =
    [
        FixedExpense(
            "telenor",
            "Telenor",
            Category.Subscription),

        FixedExpense(
            "yousee",
            "YouSee",
            Category.Subscription),

        FixedExpense(
            "norlys",
            "Norlys",
            Category.Subscription,
            ComparingType.Word),

        FixedExpense(
            "waoo",
            "Waoo",
            Category.Subscription,
            ComparingType.Word),

        FixedExpense(
            "hiper",
            "Hiper",
            Category.Subscription,
            ComparingType.Word),

        FixedExpense(
            "eesy",
            "eesy",
            Category.Subscription,
            ComparingType.Word),

        FixedExpense(
            "oister",
            "Oister",
            Category.Subscription)
    ];
}