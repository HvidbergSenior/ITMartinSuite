using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class CarRules
{
    public static readonly List<TransactionRule> Items =
    [
      

        FixedExpense(
            "dmr",
            "Motorregister",
            Category.BilVedligehold,
            ComparingType.Word)
     
    ];
}