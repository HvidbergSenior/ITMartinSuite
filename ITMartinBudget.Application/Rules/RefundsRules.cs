using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class RefundsRules
{
    public static readonly List<TransactionRule> Items =
    [
        Refund(
            "alka forsikring",
            "Alka Refusion",
            ComparingType.Exact),
        Refund(
            "udbetaling norlys",
            "Norlys Refusion",
            ComparingType.Exact),
        Refund(
            "returns",
            "Refund",
            ComparingType.Contains),

        Refund(
            "refund",
            "Refund",
            ComparingType.Contains),
        Refund(
        "danmark",
        "Danmark Refund",
        ComparingType.Exact),
    ];
}