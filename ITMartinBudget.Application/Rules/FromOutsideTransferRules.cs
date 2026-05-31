using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;
using static ITMartinBudget.Application.Rules.RulesFactory;

namespace ITMartinBudget.Application.Rules;

public static class FromOutsideTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        TransfersOutsideToUs(
            "mobilepay marianne hvidberg",
            "Marianne",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay dorthe moeller",
            "Dorthe",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay bent moeller",
            "Bent",
            ComparingType.Exact),
        TransfersOutsideFromUs(
            "mobilepay alex snaer gunnarsson",
            "Alex Snær Gunnarsson",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay mathilde caroline",
            "Mathilde-Caroline",
            ComparingType.Exact),

   
        TransfersOutsideToUs(
            "mobilepay thomas fug",
            "Thomas Fug",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay trine marie vinkler",
            "Trine Marie Vinkler",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay malene hvidberg",
            "Malene Hvidberg",
            ComparingType.Exact),
        
        TransfersOutsideToUs(
            "mobilepay tobias norman jensen",
            "Tobias Norman Jensen",
            ComparingType.Exact),
    ];
}