using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class VariableIncomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        FromKommuneAndStat(
            "feriepenge",
            "Feriepenge",
            Category.KommuneAndStat,
            ComparingType.Contains),

        FromKommuneAndStat(
            "bonus",
            "Bonus",
            Category.KommuneAndStat,
            ComparingType.Contains),

        
        FromKommuneAndStat(
            "foedevarecheck",
            "Fødevarecheck",
            Category.KommuneAndStat,
            ComparingType.Contains),

        FromKommuneAndStat(
            "aarhus kommune",
            "Fra Århus kommune",
            Category.KommuneAndStat,
            ComparingType.Contains),
        
        InterestsAndStock(
            "udbytte",
            "Udbytte",
            ComparingType.Word),
        
        FromKommuneAndStat(
        "velliv foreningen",
        "Velliv Foreningen",
        Category.KommuneAndStat,
        ComparingType.Contains),
    ];
}