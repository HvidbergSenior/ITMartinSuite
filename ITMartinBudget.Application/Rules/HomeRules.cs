using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        ThingsOtherThanClothes(
            "ikea",
            "IKEA",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "ilva",
            "ILVA",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "silvan",
            "Silvan",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "vvs eksperten",
            "VVS Eksperten",
            Category.Hjem),

        ThingsOtherThanClothes(
            "kop og kande",
            "Kop & Kande",
            Category.Hjem),

        ThingsOtherThanClothes(
            "jem og fix",
            "Jem & Fix",
            Category.Hjem),

        ThingsOtherThanClothes(
            "bauhaus",
            "Bauhaus",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "harald nyborg",
            "Harald Nyborg",
            Category.Hjem),

        ThingsOtherThanClothes(
            "biltema",
            "Biltema",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "plantorama",
            "Plantorama",
            Category.Hjem),

        ThingsOtherThanClothes(
            "jysk",
            "JYSK",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "imerco",
            "Imerco",
            Category.Hjem,
            ComparingType.Word),

        ThingsOtherThanClothes(
            "soestrene grene",
            "Søstrene Grene",
            Category.Hjem),

        ThingsOtherThanClothes(
            "hyldedeluxe",
            "HyldeDeluxe",
            Category.Hjem),

        ThingsOtherThanClothes(
            "boligmontering",
            "Boligmontering",
            Category.Hjem)
    ];
}