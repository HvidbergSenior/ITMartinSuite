using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FamilyTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.TransfersFamilyToUs(
            "mobilepay eigil hvidberg",
            "Eigil",
            ComparingType.Contains),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay bertil hvidberg",
            "Bertil",
            ComparingType.Contains),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay julius hvidberg",
            "Julius",
            ComparingType.Contains),

        // Family transfers

        RulesFactory.TransfersFamilyFromUs(
            "mobilepay bertil hvidberg",
            "Bertil",
            ComparingType.Contains),

        RulesFactory.TransfersFamilyFromUs(
            "mobilepay eigil hvidberg",
            "Eigil",
            ComparingType.Contains),

        RulesFactory.TransfersFamilyFromUs(
            "mobilepay julius hvidberg",
            "Julius",
            ComparingType.Contains),
        RulesFactory.TransfersFamilyToUs(
        "bertil hvi",
        "Bertil",
        ComparingType.Contains),
    ];
}