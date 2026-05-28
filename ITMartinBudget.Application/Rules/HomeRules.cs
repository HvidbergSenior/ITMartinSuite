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
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "ilva",
            "ILVA",
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "silvan",
            "Silvan",
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "vvs eksperten",
            "VVS Eksperten",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "kop og kande",
            "Kop & Kande",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "jem og fix",
            "Jem & Fix",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "bauhaus",
            "Bauhaus",
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "harald nyborg",
            "Harald Nyborg",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "biltema",
            "Biltema",
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "plantorama",
            "Plantorama",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "jysk",
            "JYSK",
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "imerco",
            "Imerco",
            Category.Hjem,
            ComparingType.Word),

        GeneralShopping(
            "soestrene grene",
            "Søstrene Grene",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "hyldedeluxe",
            "HyldeDeluxe",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "boligmontering",
            "Boligmontering",
            Category.Hjem,
            ComparingType.Contains)
    ];
}