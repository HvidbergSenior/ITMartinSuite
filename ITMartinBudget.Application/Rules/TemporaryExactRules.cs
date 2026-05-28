using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TemporaryExactTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        TransfersOutsideToUs(
            "mobilepay anne bro friis",
            "Anne Bro Friis",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay anne bro friis jense",
            "Anne Bro Friis",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay bent moeller joh",
            "Bent Møller",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay bent moeller johnsen",
            "Bent Møller",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay bertil hvidberg",
            "Bertil",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay bertil hvidberg john",
            "Bertil",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay dorthe moeller j",
            "Dorthe Møller",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay dorthe moeller johnse",
            "Dorthe Møller",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay eigil hvidberg",
            "Eigil",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay eigil hvidberg johns",
            "Eigil",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay ida koester heil",
            "Ida Køster",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay ida koester heilbo",
            "Ida Køster",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay inge kjaerulff t",
            "Inge Kjærulff Torp",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay inge kjaerulff torp",
            "Inge Kjærulff Torp",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay julius hvidberg",
            "Julius",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay julius hvidberg john",
            "Julius",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay karsten juel bu",
            "Karsten Juel Bunch",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay karsten juel bunch",
            "Karsten Juel Bunch",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay mille ea bastho",
            "Mille Ea Bastholm",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay mille ea bastholm",
            "Mille Ea Bastholm",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay thomas fug",
            "Thomas Fug",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay tobias norman jensen",
            "Tobias Norman Jensen",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay trine marie vinkler",
            "Trine Marie Vinkler",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay alex snaer gunnarsson",
            "Alex Snær Gunnarsson",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay mathilde caroline fo",
            "Mathilde-Caroline",
            ComparingType.Exact)
    ];
}