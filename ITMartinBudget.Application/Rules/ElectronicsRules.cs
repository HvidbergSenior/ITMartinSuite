using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ElectronicsRules
{
    public static readonly List<TransactionRule> Items =
    [
        GeneralShopping(
            "proshop",
            "Proshop",
            Category.Elektronik,
            ComparingType.Contains),

        GeneralShopping(
            "elgiganten",
            "Elgiganten",
            Category.Elektronik,
            ComparingType.Contains),

        GeneralShopping(
            "power",
            "Power",
            Category.Elektronik,
            ComparingType.Word),

        GeneralShopping(
            "humac",
            "Humac",
            Category.Elektronik,
            ComparingType.Contains),

        GeneralShopping(
            "fonik",
            "Fonik",
            Category.Elektronik,
            ComparingType.Contains),

        GeneralShopping(
            "av cable",
            "AV-Cables",
            Category.Elektronik,
            ComparingType.Contains),

        GeneralShopping(
            "computersalg",
            "ComputerSalg",
            Category.Elektronik,
            ComparingType.Contains),

        GeneralShopping(
            "komplett",
            "Komplett",
            Category.Elektronik,
            ComparingType.Contains)
    ];
}