using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;
using static ITMartinBudget.Application.Rules.RulesFactory;

namespace ITMartinBudget.Application.Rules;

public static class BentSonjaRules
{
    public static readonly List<TransactionRule> Items =
    [
        
        TransfersOutsideToUs(
            "mobilepay bent moeller",
            "Bent",
            ComparingType.Exact),
        RulesFactory.TransfersOutsideToUs(
            "mobilepay sonja johnsen",
            "Sonja",
            ComparingType.Exact),

      
        RulesFactory.TransfersOutsideToUs(
            "mobilepay bert moeller joh",
            "Bert Møller",
            ComparingType.Contains),
        RulesFactory.TransfersOutsideToUs(
            "mobilepay sonja johnsen",
            "Sonja Johnsen",
            ComparingType.Exact),
        
    ];
}