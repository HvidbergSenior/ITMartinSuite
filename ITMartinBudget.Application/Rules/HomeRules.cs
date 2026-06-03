using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        GeneralShopping(
            "ikea",
            "IKEA",
            Category.Bolig,
            ComparingType.Word),

        GeneralShopping(
            "ilva",
            "ILVA",
            Category.Bolig,
            ComparingType.Word),

        GeneralShopping(
            "silvan",
            "Silvan",
            Category.Bolig,
            ComparingType.Word),

        HomeRepair(
            "vvs eksperten",
            "VVS Eksperten",
            Category.BoligVedligehold,
            ComparingType.Contains),

        GeneralShopping(
            "kop og kande",
            "Kop & Kande",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "jem og fix",
            "Jem & Fix",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "bauhaus",
            "Bauhaus",
            Category.Bolig,
            ComparingType.Word),

        GeneralShopping(
            "harald nyborg",
            "Harald Nyborg",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "biltema",
            "Biltema",
            Category.Bolig,
            ComparingType.Word),

        GeneralShopping(
            "plantorama",
            "Plantorama",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "jysk",
            "JYSK",
            Category.Bolig,
            ComparingType.Word),

        GeneralShopping(
            "imerco",
            "Imerco",
            Category.Bolig,
            ComparingType.Word),

        GeneralShopping(
            "soestrene grene",
            "Søstrene Grene",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "hyldedeluxe",
            "HyldeDeluxe",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "boligmontering",
            "Boligmontering",
            Category.Bolig,
            ComparingType.Contains)
    ];
}