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
            ComparingType.Exact),
        RulesFactory.TransfersFamilyFromUs(
            "mobilepay bertil hvidberg john",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyFromUs(
            "mobilepay julius hvidberg john",
            "Julius",
            ComparingType.Exact),
        RulesFactory.TransfersFamilyToUs(
            "mobilepay bertil hvi",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay bertil hvidberg",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay eigil hvidberg",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay eigil hvidberg johns",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay julius hvi",
            "Julius",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "mobilepay julius hvidberg",
            "Julius",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "vdk mob pay bertil hvidberg j",
            "Bertil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "vdk mob pay eigil hvidberg jo",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "vdk mob pay julius hvidberg j",
            "Julius",
            ComparingType.Exact),
        
        RulesFactory.InternalAccountTransfer(
            "overfoersel",
            "Overførsel",
            Category.Overfoersel,
            ComparingType.Exact),
    ];
}