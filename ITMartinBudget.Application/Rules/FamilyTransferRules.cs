using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;
namespace ITMartinBudget.Application.Rules;

public static class FamilyTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.TransfersChildrenToUs(
            "mobilepay eigil hvidberg",
            "Eigil",
            ComparingType.Exact),
        
        RulesFactory.TransfersChildrenFromUs(
            "mobilepay bertil hvidberg john",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenFromUs(
            "mobilepay julius hvidberg john",
            "Julius",
            ComparingType.Exact),
        RulesFactory.TransfersChildrenToUs(
            "mobilepay bertil hvi",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenToUs(
            "mobilepay bertil hvidberg",
            "Bertil",
            ComparingType.Exact),
        
        RulesFactory.TransfersChildrenToUs(
            "mobilepay eigil hvidberg johns",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenToUs(
            "mobilepay julius hvi",
            "Julius",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenToUs(
            "mobilepay julius hvidberg",
            "Julius",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenToUs(
            "vdk mob pay bertil hvidberg j",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenToUs(
            "vdk mob pay eigil hvidberg jo",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersChildrenToUs(
            "vdk mob pay julius hvidberg j",
            "Julius",
            ComparingType.Exact),
    ];
}