using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TaxRules
{
    public static readonly List<TransactionRule> Items =
    [
        Taxes(
            "sktst motor",
            "Motorafgift",
            Category.BilVedligehold, ComparingType.Contains),
    ];
}