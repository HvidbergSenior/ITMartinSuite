using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HousingRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Mortgage / Housing

        RealkreditSkatBolig(
            "termin jyske realkredit",
            "Jyske Realkredit",
            Category.Husleje,
            ComparingType.Contains),

        RealkreditSkatBolig(
            "aarhus kommune ejendomsskat",
            "Ejendomsskat",
            Category.Bolig,
            ComparingType.Contains),

        RealkreditSkatBolig(
            "parcelforening",
            "Parcelforening",
            Category.Bolig,
            ComparingType.Exact,12),

        RealkreditSkatBolig(
            "skattestyrelsen",
            "Skattestyrelsen",
            Category.Skat,
            ComparingType.Contains),

        // Utilities

        RealkreditSkatBolig(
            "aarhus vand",
            "Aarhus Vand",
            Category.BoligVedligehold,
            ComparingType.Contains, 4),

        RealkreditSkatBolig(
            "bs kredsloeb a/s",
            "Kredsløb",
            Category.BoligVedligehold,
            ComparingType.Exact, 3),
        RealkreditSkatBolig(
            "bs kredsloeb genbrug a/s",
            "Kredsløb",
            Category.BoligVedligehold,
            ComparingType.Exact, 6),
        
        RealkreditSkatBolig(
            "andel energi",
            "Andel Energi",
            Category.BoligVedligehold,
            ComparingType.Contains),

        RealkreditSkatBolig(
            "nrgi",
            "NRGi Elhandel A/S",
            Category.BoligVedligehold,
            ComparingType.Word,1)
    ];
}