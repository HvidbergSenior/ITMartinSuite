using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ElectronicsRules
{
    public static readonly List<TransactionRule> Items =
    [
        WorkExpense(
            "proshop",
            "Proshop",
            Category.Elektronik,
            ComparingType.Contains),

        WorkExpense(
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

        WorkExpense(
            "av cable",
            "AV-Cables",
            Category.Elektronik,
            ComparingType.Contains),

        WorkExpense(
            "computersalg",
            "ComputerSalg",
            Category.Elektronik,
            ComparingType.Contains),

        WorkExpense(
            "komplett",
            "Komplett",
            Category.Elektronik,
            ComparingType.Contains)
    ];
}