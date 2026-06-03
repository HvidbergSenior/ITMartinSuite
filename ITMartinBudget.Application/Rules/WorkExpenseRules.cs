using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class WorkExpenseRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.WorkExpense(
            "vdk jetbrains",
            "JetBrains",
            Category.Subscription,
            ComparingType.Exact),

        RulesFactory.WorkExpense(
            "vdk one com",
            "One.com",
            Category.TelefonTvInternet,
            ComparingType.Exact),
    ];
}