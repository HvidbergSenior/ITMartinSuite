using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FixedIncomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        FixedIncome(
            "loenoverfoersel",
            "Salary"),

        FixedIncome(
            "maanedsloen",
            "Salary"),

        FixedIncome(
            "plusloen",
            "Salary"),

        FixedIncome(
            "loen",
            "Salary",
            ComparingType.Word),
        
        RulesFactory.FixedIncome(
        "su",
        "SU",
        ComparingType.Exact)
    ];
}