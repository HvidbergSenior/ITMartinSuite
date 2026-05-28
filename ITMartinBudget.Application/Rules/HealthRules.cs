using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HealthRules
{
    public static readonly List<TransactionRule> Items =
    [
        PersonalCare(
            "tandlaege",
            "Tandlæge",
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "apotek",
            "Apotek",
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "synoptik",
            "Synoptik",
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "profil optik",
            "Profil Optik",
            Category.Sundhed,
            ComparingType.Contains),

        PersonalCare(
            "aarhus tandcenter",
            "Aarhus Tandcenter",
            Category.Sundhed,
            ComparingType.Contains)
    ];
}