using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class PetsRules
{
    public static readonly List<TransactionRule> Items =
    [
        GeneralShopping(
            "zooplus",
            "Zooplus",
            Category.Kaeledyr,
            ComparingType.Contains),

        GeneralShopping(
            "anicura",
            "AniCura",
            Category.Kaeledyr,
            ComparingType.Contains),

        GeneralShopping(
            "dyrlaege",
            "Dyrlæge",
            Category.Kaeledyr,
            ComparingType.Contains),

        GeneralShopping(
            "maxizoo",
            "Maxi Zoo",
            Category.Kaeledyr,
            ComparingType.Contains),

        GeneralShopping(
            "petworld",
            "Petworld",
            Category.Kaeledyr,
            ComparingType.Contains)
    ];
}