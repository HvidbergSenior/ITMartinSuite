using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class PetsRules
{
    public static readonly List<TransactionRule> Items =
    [
        Pets(
            "zooplus",
            "Zooplus"),

        Pets(
            "anicura",
            "AniCura"),

        Pets(
            "dyrlaege",
            "Dyrlæge"),

        Pets(
            "maxizoo",
            "Maxi Zoo"),

        Pets(
            "petworld",
            "Petworld")
    ];
}