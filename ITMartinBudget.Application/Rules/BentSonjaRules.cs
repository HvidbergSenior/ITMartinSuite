using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;
using static ITMartinBudget.Application.Rules.RulesFactory;

namespace ITMartinBudget.Application.Rules;

public static class BentSonjaRules
{
    public static readonly List<TransactionRule> Items =
    [
        
        BentOgSonjaInd(
            "mobilepay bent moeller",
            "Bent",
            Category.OverfoerselFraSonjaBent,
            ComparingType.Exact),
        
        BentOgSonjaInd(
            "mobilepay sonja johnsen",
            "Sonja",
            Category.OverfoerselFraSonjaBent,
            ComparingType.Exact),
      
        BentOgSonjaInd(
            "mobilepay bert moeller joh",
            "Bert Møller",
            Category.OverfoerselFraSonjaBent,
            ComparingType.Contains),
        RulesFactory.BentOgSonjaInd(
            "mobilepay bent moelle",
            "Bent Møller",
            Category.OverfoerselFraSonjaBent,
            
            ComparingType.Exact),

        RulesFactory.BentOgSonjaInd(
            "mobilepay bent moeller joh",
            "Bent Møller",
            Category.OverfoerselFraSonjaBent,
            
            ComparingType.Exact),

        RulesFactory.BentOgSonjaInd(
            "mobilepay bent moeller johnsen",
            "Bent Møller",
            Category.OverfoerselFraSonjaBent,
            
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay sonja john",
            "Sonja",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "vdk mob pay bent moller johns",
            "Bent Møller",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "vdk mob pay sonja johnsen",
            "Sonja Johnsen",
            ComparingType.Exact),

    ];
}