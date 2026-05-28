using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ElectronicsRules
{
    public static readonly List<TransactionRule> Items =
    [
        ElectronicsBought(
            "proshop",
            "Proshop",
            Category.Elektronik),

        ElectronicsBought(
            "elgiganten",
            "Elgiganten",
            Category.Elektronik),

        ElectronicsBought(
            "power",
            "Power",
            Category.Elektronik),

        ElectronicsBought(
            "humac",
            "Humac",
            Category.Elektronik),

        ElectronicsBought(
            "fonik",
            "Fonik",
            Category.Elektronik),

        ElectronicsBought(
            "av cable",
            "AV-Cables",
            Category.Elektronik),

        ElectronicsBought(
            "computersalg",
            "ComputerSalg",
            Category.Elektronik),

        ElectronicsBought(
            "komplett",
            "Komplett",
            Category.Elektronik)
    ];
}