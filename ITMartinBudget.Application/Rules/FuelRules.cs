using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FuelRules
{
    public static readonly List<TransactionRule> Items =
    [
        Fuel(
            "circle k",
            "Circle K",
            ComparingType.Contains),

        Fuel(
            "uno x",
            "Uno-X",
            ComparingType.Contains),

        Fuel(
            "ingo",
            "Ingo",
            ComparingType.Word),

        Fuel(
            "dk ok",
            "OK",
            ComparingType.Contains),

        Fuel(
            "vdk q8",
            "Q8",
            ComparingType.Contains),

        Fuel(
            "shell",
            "Shell",
            ComparingType.Word),

        Fuel(
            "go on",
            "Go'on",
            ComparingType.Contains),

        Fuel(
            "f24",
            "F24",
            ComparingType.Word),

        Fuel(
            "tankstation",
            "Tankstation",
            ComparingType.Contains),
        RulesFactory.Fuel(
            "vdk best romedal 0624",
            "Best",
            ComparingType.Exact),

        RulesFactory.Fuel(
            "vdk superspeed 1 c",
            "Superspeed",
            ComparingType.Exact),
    ];
}