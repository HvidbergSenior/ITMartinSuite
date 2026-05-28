using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HousingRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Mortgage / Housing

        FixedExpense(
            "termin jyske realkredit",
            "Jyske Realkredit",
            Category.Bolig),

        FixedExpense(
            "aarhus kommune ejendomsskat",
            "Ejendomsskat",
            Category.Bolig),

        FixedExpense(
            "parcelforeningen",
            "Parcelforening",
            Category.Bolig),

        FixedExpense(
            "skattestyrelsen",
            "Skattestyrelsen",
            Category.Bolig),

        // Utilities

        FixedExpense(
            "aarhus vand",
            "Aarhus Vand",
            Category.Bolig),

        FixedExpense(
            "kredsloeb",
            "Kredsløb",
            Category.Bolig,
            ComparingType.Word),

        FixedExpense(
            "norlys energi",
            "Norlys Energi",
            Category.Bolig),

        FixedExpense(
            "ewii",
            "EWII",
            Category.Bolig,
            ComparingType.Word),

        FixedExpense(
            "andel energi",
            "Andel Energi",
            Category.Bolig),

        FixedExpense(
            "nrgi",
            "NRGi Elhandel A/S",
            Category.Bolig,
            ComparingType.Word)
    ];
}