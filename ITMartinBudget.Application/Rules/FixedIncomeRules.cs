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
            "AutoproffLøn",
            Category.Løn,
            ComparingType.Contains),
        Salary(
            "loenover 3009771645",
            "Dagpenge",
            Category.Løn,
            
            ComparingType.Exact),
        Salary(
            "maanedsloen",
            "Løn",
            Category.Løn,
            
            ComparingType.Contains),

        Salary(
            "plusloen",
            "PlusLøn",
            Category.Løn,
            
            ComparingType.Contains),

        Salary(
            "loen",
            "VibzLøn",
            Category.Løn,
            
            ComparingType.Exact),

        Su(
            "su",
            "SU",
            ComparingType.Exact)
    ];
}