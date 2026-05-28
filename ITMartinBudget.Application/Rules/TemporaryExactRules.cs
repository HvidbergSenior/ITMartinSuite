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
            ComparingType.Exact),
        EverydayGrocery(
            "dk lidl",
            "Lidl",
            ComparingType.Exact),

        TransfersFamilyToUs(
            "mobilepay eigil hvid",
            "Eigil",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay bent moelle",
            "Bent Møller",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay malene hvi",
            "Malene Hvidberg",
            ComparingType.Exact),

        ClothesAndShoes(
            "vdk hm dk0844",
            "H&M",
            Category.Toej,
            ComparingType.Exact),

        Entertainment(
            "vdk steamgames com 4259522985",
            "Steam",
            Category.Gaming,
            ComparingType.Exact),

        FixedExpense(
            "parcelforening",
            "Parcelforening",
            Category.Bolig,
            ComparingType.Exact),

        GeneralShopping(
            "dk kop kande web aps",
            "Kop & Kande",
            Category.Hjem,
            ComparingType.Exact),

        EverydayGrocery(
            "vdk reenberg gront",
            "Reenberg Grønt",
            ComparingType.Exact),

        GiftFromUs(
            "mobilepay 1 000 tak for din hj",
            "Donation",
            ComparingType.Exact),

        GiftFromUs(
            "mobilepay blaa kors genbrug a",
            "Blå Kors",
            ComparingType.Exact),

        Refund(
            "danmark",
            "Danmark Refund",
            ComparingType.Exact),
        TransfersOutsideToUs(
            "mobilepay mette clemmense",
            "Mette Clemmense",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay jesper mahler clemme",
            "Jesper Mahler Clemmensen",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay rikke visby bunch",
            "Rikke Visby Bunch",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay lisbeth krogshede",
            "Lisbeth Krogshede",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay mads gjellerod",
            "Mads Gjellerod",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay lisbeth kr",
            "Lisbeth",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay dorthe don",
            "Dorthe Donbæk",
            ComparingType.Exact),
    ];
}