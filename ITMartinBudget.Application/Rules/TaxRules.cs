using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TaxRules
{
    public static readonly List<TransactionRule> Items =
    [
        CarMaintenance(
            "BS SKATTESTYRELSEN MOTOR OPKRÆVNING",
            "Motorafgift",
            Category.BilVedligehold, ComparingType.Contains, 6),
        
        Taxes(
            "dk sktst personskatter",
            "Overskydende Skat",
            Category.Skat,
            ComparingType.Exact),
        Taxes(
            "overskydende skat",
            "Overskydende Skat",
            Category.Skat,
            ComparingType.Contains),

    ];
}