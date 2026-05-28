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
            Category.FagforeningAKasse),

        UnionAndAKasse(
            "socialpaedagogernes landsforbund",
            "Socialpædagogernes Landsforbund",
            Category.FagforeningAKasse),

        UnionAndAKasse(
            "3f",
            "3F",
            Category.FagforeningAKasse,
            ComparingType.Word),

        UnionAndAKasse(
            "hk",
            "HK",
            Category.FagforeningAKasse,
            ComparingType.Word),

        UnionAndAKasse(
            "dlf",
            "Danmarks Lærerforening",
            Category.FagforeningAKasse,
            ComparingType.Word)
        
        
    ];
}