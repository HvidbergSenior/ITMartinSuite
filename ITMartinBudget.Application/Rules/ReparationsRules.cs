using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ReparationsRules
{
    public static readonly List<TransactionRule> Items =
    [
        OtherRepairThanCar(
            "den store cykelsmed",
            "Den Store Cykelsmed"),

        CarRepair(
            "sejer",
            "Sejer"),

        CarRepair(
            "p christensen",
            "P. Christensen"),
        
        CarRepair(
        "thansen",
        "thansen"),

        CarRepair(
            "sejr jensen auto",
            "Sejr Jensen Auto",
            Category.BilVedligehold),

        CarRepair(
            "quickpot",
            "QuickPot",
            Category.BilVedligehold),

        CarRepair(
            "euromaster",
            "Euromaster",
            Category.BilVedligehold),

        CarRepair(
            "autobutler",
            "AutoButler",
            Category.BilVedligehold),

        CarRepair(
            "skorstensgaard",
            "Skorstensgaard",
            Category.BilVedligehold),

        // Vehicle Tax

        
    ];
}