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

    ];
}