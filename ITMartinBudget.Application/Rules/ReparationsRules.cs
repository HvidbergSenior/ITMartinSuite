using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ReparationsRules
{
    public static readonly List<TransactionRule> Items =
    [
        GeneralShopping(
            "den store cykelsmed",
            "Den Store Cykelsmed",
            Category.BoligVedligehold,
            ComparingType.Contains),

        HomeRepair(
            "Hvidevarehjælpen",
            "Hvidevarehjælpen",
            Category.BoligVedligehold,
            ComparingType.Contains),
        HomeRepair(
            "hvidevarehjaelpen rep vaskemaskin",
            "Hvidevarehjælpen",
            Category.BoligVedligehold,
            ComparingType.Exact),
        HomeRepair(
            "Hvidevarehjælpen rep vaskemaskin",
            "Hvidevarehjælpen",
            Category.BoligVedligehold,
            ComparingType.Exact),
        CarRepair(
            "sejer",
            "Sejer",
            Category.BilVedligehold,
            ComparingType.Word),

        CarRepair(
            "p christensen",
            "P. Christensen",
            Category.BilVedligehold,
            ComparingType.Contains),

        CarRepair(
            "thansen",
            "thansen",
            Category.BilVedligehold,
            ComparingType.Contains),

        CarRepair(
            "sejr jensen auto",
            "Sejer",
            Category.BilVedligehold,
            ComparingType.Contains),

        CarRepair(
            "quickpot",
            "QuickPot",
            Category.BilVedligehold,
            ComparingType.Contains),

        CarRepair(
            "euromaster",
            "Euromaster",
            Category.BilVedligehold,
            ComparingType.Contains),

        CarRepair(
            "autobutler",
            "AutoButler",
            Category.BilVedligehold,
            ComparingType.Contains),

        CarRepair(
            "skorstensgaard",
            "Skorstensgaard",
            Category.BilVedligehold,
            ComparingType.Contains),
        RulesFactory.CarRepair(
            "mekaniker",
            "Mekaniker",
            Category.BilVedligehold,
            ComparingType.Exact),


    ];
}