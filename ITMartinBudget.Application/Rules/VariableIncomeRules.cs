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
            Category.KommuneAndStat),

        FromKommuneAndStat(
            "bonus",
            "Bonus",
            Category.KommuneAndStat),

        Taxes(
            "overskydende skat",
            "Tax Return",
            Category.Skat),

        FromKommuneAndStat(
            "foedevarecheck",
            "Government Support",
            Category.KommuneAndStat),

        FromKommuneAndStat(
            "aarhus kommune",
            "Government Income",
            Category.KommuneAndStat),

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