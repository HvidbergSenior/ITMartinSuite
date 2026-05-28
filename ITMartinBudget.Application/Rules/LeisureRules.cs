using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class LeisureRules
{
    public static readonly List<TransactionRule> Items =
    [
        Entertainment(
            "universal music",
            "Universal Music",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "fof aarhus",
            "FOF Aarhus",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "klitmoeller",
            "Klitmøller",
            Category.Rejse,
            ComparingType.Contains),

        GeneralShopping(
            "radisson blu",
            "Radisson Blu",
            Category.Cafe,
            ComparingType.Contains),

        GeneralShopping(
            "malerfirma tidens farver",
            "Tidens Farver",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "skumhuset",
            "Skumhuset",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "chokolet",
            "Chokolade",
            Category.Cafe,
            ComparingType.Contains),

        GeneralShopping(
            "noeddebutikken",
            "Nøddebutikken",
            Category.Cafe,
            ComparingType.Contains),

        GeneralShopping(
            "roede kors butik",
            "Røde Kors Butik",
            Category.Toej,
            ComparingType.Contains),

        GeneralShopping(
            "vesterlund efterskol",
            "Vesterlund Efterskole",
            Category.Boern,
            ComparingType.Contains),

        Entertainment(
            "fastelavnsbazar",
            "Fastelavnsbazar",
            Category.Fritid,
            ComparingType.Contains)
    ];
}