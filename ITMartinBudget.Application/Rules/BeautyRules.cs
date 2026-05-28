using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class BeautyRules
{
    public static readonly List<TransactionRule> Items =
    [
        PersonalCare(
            "piet",
            "Frisør Piet",
            Category.Sundhed,
            ComparingType.Word),

        PersonalCare(
            "lyko",
            "Lyko",
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "matas",
            "Matas",
            Category.Sundhed,
            ComparingType.Word),

        PersonalCare(
            "normal",
            "Normal",
            Category.Sundhed,
            ComparingType.Word),

        PersonalCare(
            "magasin",
            "Magasin Beauty",
            Category.Sundhed,
            ComparingType.Word),

        PersonalCare(
            "sephora",
            "Sephora",
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "nicehair",
            "NiceHair"
            ,
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "frisoer",
            "Frisør",
            Category.Sundhed,
            ComparingType.Word),
        PersonalCare(
            "norregades apot",
            "Apotek",
            Category.Sundhed,
            ComparingType.Contains),
    ];
}