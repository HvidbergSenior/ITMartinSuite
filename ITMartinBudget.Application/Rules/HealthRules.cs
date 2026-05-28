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
            Category.Sundhed),

        PersonalCare(
            "apotek",
            "Apotek",
            Category.Sundhed),

        PersonalCare(
            "synoptik",
            "Synoptik",
            Category.Sundhed),

        PersonalCare(
            "profil optik",
            "Profil Optik",
            Category.Sundhed),

        PersonalCare(
            "aarhus tandcenter",
            "Aarhus Tandcenter",
            Category.Sundhed)
    ];
}