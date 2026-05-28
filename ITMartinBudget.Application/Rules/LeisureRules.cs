using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class LeisureRules
{
    public static readonly List<TransactionRule> Items =
    [
        EntertainmentExpense(
            "universal music",
            "Universal Music",
            Category.Fritid),

        EntertainmentExpense(
            "fof aarhus",
            "FOF Aarhus",
            Category.Fritid),

        EntertainmentExpense(
            "klitmoeller",
            "Klitmøller",
            Category.Rejse),

        ThingsOtherThanClothes(
            "radisson blu",
            "Radisson Blu",
            Category.Cafe),

        ThingsOtherThanClothes(
            "malerfirma tidens farver",
            "Tidens Farver",
            Category.Hjem),

        ThingsOtherThanClothes(
            "skumhuset",
            "Skumhuset",
            Category.Fritid),

        EntertainmentExpense(
            "chokolet",
            "Chokolade",
            Category.Cafe),

        ThingsOtherThanClothes(
            "noeddebutikken",
            "Nøddebutikken",
            Category.Cafe),

        ThingsOtherThanClothes(
            "roede kors butik",
            "Røde Kors Butik",
            Category.Toej),

        ThingsOtherThanClothes(
            "vesterlund efterskol",
            "Vesterlund Efterskole",
            Category.Boern)
    ];
}