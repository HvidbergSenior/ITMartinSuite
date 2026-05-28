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

        Taxes(
            "overskydende skat",
            "Tax Return",
            Category.Skat,
            ComparingType.Contains),

        FromKommuneAndStat(
            "foedevarecheck",
            "Government Support",
            Category.KommuneAndStat,
            ComparingType.Contains),

        FromKommuneAndStat(
            "aarhus kommune",
            "Government Income",
            Category.KommuneAndStat,
            ComparingType.Contains),

        InterestsAndStock(
            "rente",
            "Interest",
            ComparingType.Word),

        InterestsAndStock(
            "udbytte",
            "Dividend",
            ComparingType.Word)
    ];
}