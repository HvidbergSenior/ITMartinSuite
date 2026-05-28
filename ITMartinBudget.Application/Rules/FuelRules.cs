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
            "Circle K"),

        Fuel(
            "uno x",
            "Uno-X"),

        Fuel(
            "ingo",
            "Ingo"),

        Fuel(
            "dk ok",
            "OK"),

        Fuel(
            "vdk q8",
            "Q8"),

        Fuel(
            "shell",
            "Shell"),

        Fuel(
            "go on",
            "Go'on"),

        Fuel(
            "f24",
            "F24"),

        Fuel(
            "tankstation",
            "Tankstation")
    ];
}