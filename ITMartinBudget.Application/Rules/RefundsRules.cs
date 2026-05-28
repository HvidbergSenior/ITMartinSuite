using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class RefundsRules
{
    public static readonly List<TransactionRule> Items =
    [
        Refund(
            "returns",
            "Refund"),

        Refund(
            "refund",
            "Refund")
    ];
}