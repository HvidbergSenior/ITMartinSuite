using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FixedIncomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        Salary(
            "loenoverfoersel",
            "Salary",
            ComparingType.Contains),

        Salary(
            "maanedsloen",
            "Salary",
            ComparingType.Contains),

        Salary(
            "plusloen",
            "Salary",
            ComparingType.Contains),

        Salary(
            "loen",
            "Salary",
            ComparingType.Word),

        Su(
            "su",
            "SU",
            ComparingType.Exact)
    ];
}