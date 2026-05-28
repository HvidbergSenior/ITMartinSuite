using ITMartinBudget.Application.Models;
using ITMartinBudget.Application.Models;

using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FamilyTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.TransfersFromFamily(
            "mobilepay eigil hvidberg",
            "Eigil"),

        RulesFactory.TransfersFromFamily(
            "mobilepay bertil hvidberg",
            "Bertil"),

        RulesFactory.TransfersFromFamily(
            "mobilepay julius hvidberg",
            "Julius"),

        // Family transfers

        RulesFactory.TransfersToFamily(
            "mobilepay bertil hvidberg",
            "Bertil"),

        RulesFactory.TransfersToFamily(
            "mobilepay eigil hvidberg",
            "Eigil"),

        RulesFactory.TransfersToFamily(
            "mobilepay julius hvidberg",
            "Julius"),

        
    ];
}