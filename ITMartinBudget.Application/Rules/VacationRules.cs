using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class VacationRules
{
    public static readonly List<TransactionRule> Items =
    [
        RejserUdflugter(
            "Seawest",
            "SeaWest",
            Category.RejserUdflugter,
            ComparingType.Word),

    ];
}