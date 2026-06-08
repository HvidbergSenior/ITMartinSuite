using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class UnionRules
{
    public static readonly List<TransactionRule> Items =
    [
        UnionAndAKasse(
            "akademikernes a kasse",
            "Akademikernes A-Kasse",
            Category.FagforeningAKasse,
            ComparingType.Contains, 3),

        UnionAndAKasse(
            "socialpaedagogernes landsforbund",
            "Socialpædagogernes Landsforbund",
            Category.FagforeningAKasse,
            ComparingType.Contains),
        
       
        RulesFactory.UnionAndAKasse(
            "101 udbet fra 3fa",
            "3F",
            Category.FagforeningAKasse,
            ComparingType.Exact),
        
        RulesFactory.UnionAndAKasse(
            "til 3f kontingent",
            "3F",
            Category.FagforeningAKasse,
            ComparingType.Exact, 1),
        
        RulesFactory.UnionAndAKasse(
            "bs fagligt faelles forbund",
            "3F",
            Category.FagforeningAKasse,
            ComparingType.Exact, 1),
    ];
}