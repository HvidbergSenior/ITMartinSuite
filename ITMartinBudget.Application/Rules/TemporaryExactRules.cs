using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TemporaryExactTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        TransfersOutsideToUs(
            "mobilepay ida koester heil",
            "Ida Køster",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay ida koester heilbo",
            "Ida Køster",
            ComparingType.Exact),
        

        TransfersFamilyToUs(
            "mobilepay julius hvidberg",
            "Julius",
            ComparingType.Exact),

        

        TransfersOutsideToUs(
            "mobilepay karsten juel bu",
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
            "vdk reop kand gront",
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
            "mobblaa korsense",
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
            "mobilepay liikke visby bunede",
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
          // =====================================
        // FAMILY / MOBILEPAY
        // =====================================

        RulesFactory.TransfersFamilyToUs(
            "vdk mob pay eigil hvidberg jo",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersFamilyToUs(
            "vdk mob pay julius hvidberg j",
            "Julius",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mona olin hvidberg",
            "Mona",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mona olin hvidb",
            "Mona",
            ComparingType.Contains),

        // =====================================
        // SUBSCRIPTIONS
        // =====================================

        RulesFactory.WorkExpense(
            "vdk one com",
            "One.com",
            Category.TelefonTvInternet,
            ComparingType.Exact),

        RulesFactory.WorkExpense(
            "vdk jetbrains",
            "JetBrains",
            Category.Subscription,
            ComparingType.Exact),

        RulesFactory.Subscription(
            "dk story house egmont a s",
            "Story House Egmont",
            Category.Fritid,
            ComparingType.Exact),

        // =====================================
        // TRANSPORT
        // =====================================

        RulesFactory.Parking(
            "vdk epass24 com",
            "Epass24",
            ComparingType.Exact),

        RulesFactory.Fuel(
            "vdk best romedal 0624",
            "Best",
            ComparingType.Exact),

        // =====================================
        // GROCERIES
        // =====================================

        RulesFactory.EverydayGrocery(
            "dk spar skejby",
            "Spar",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk kiwi 025 romedal",
            "Kiwi",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "dk coop kv vericentret",
            "Coop",
            ComparingType.Exact),

        // =====================================
        // CLOTHES
        // =====================================

        RulesFactory.ClothesAndShoes(
            "dk hennes mauritz 844",
            "H&M",
            Category.Toej,
            ComparingType.Exact),

        // =====================================
        // UNION / TAX
        // =====================================

        RulesFactory.UnionAndAKasse(
            "bs fagligt faelles forbund",
            "3F",
            Category.FagforeningAKasse,
            ComparingType.Exact),

        RulesFactory.Taxes(
            "dk sktst personskatter",
            "SKAT",
            Category.Skat,
            ComparingType.Exact),

        // =====================================
        // CAR
        // =====================================

        RulesFactory.CarRepair(
            "mekaniker",
            "Mekaniker",
            Category.BilVedligehold,
            ComparingType.Exact),

        // =====================================
        // SHOPPING
        // =====================================

        RulesFactory.GeneralShopping(
            "kattemad",
            "Kattemad",
            Category.Kaeledyr,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "tm materialer",
            "TM Materialer",
            Category.OtherThanGroceries,
            ComparingType.Exact),

        // =====================================
        // INCOME
        // =====================================

        RulesFactory.UnionAndAKasse(
            "101 udbet fra 3fa",
            "3F",
            Category.KommuneAndStat,
            ComparingType.Exact),
        RulesFactory.Fuel(
            "vdk superspeed 1 c",
            "Superspeed",
            ComparingType.Exact),

        RulesFactory.Subscription(
            "mobilepay bedre psykiatri",
            "Donation",
            Category.Subscription,
            ComparingType.Exact),

        RulesFactory.RestaurantCafe(
            "vdk restaurant storm",
            "Restaurant Storm",
            Category.Restaurant,
            ComparingType.Exact),

        RulesFactory.RestaurantCafe(
            "vdk cafe vestergade 42 aps",
            "Cafe Vestergade",
            Category.Cafe,
            ComparingType.Exact),

        RulesFactory.Subscription(
            "teleno32107104134621",
            "Telenor",
            Category.TelefonTvInternet,
            ComparingType.Exact),

        RulesFactory.Entertainment(
            "vdk sp royalcdkeys",
            "RoyalCDKeys",
            Category.Gaming,
            ComparingType.Exact),
        RulesFactory.TransfersOutsideToUs(
    "mobilepay mathias olin hv",
    "Mathias Olin",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorte rindom jeppese",
    "Dorte Rindom",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "vdk mob pay sonja johnsen",
    "Sonja Johnsen",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "vdk mob pay bent moller johns",
    "Bent Møller",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay andreas benjamin gjoe",
    "Andreas Benjamin",
    ComparingType.Exact),

RulesFactory.TransfersOutsideFromUs(
    "vdk mob pay christian birkefe",
    "Christian",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay clara rindom hoe",
    "Clara",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay lisbeth krogshe",
    "Lisbeth",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay peter lau torst niel",
    "Peter Nielsen",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay allan carl erik",
    "Allan",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay per nielsen",
    "Per Nielsen",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorte rindom je",
    "Dorte Rindom",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay eik nysom boeg j",
    "Eik",
    ComparingType.Exact),

RulesFactory.TransfersOutsideFromUs(
    "vdk mob pay christina aitken",
    "Christina",
    ComparingType.Exact),

RulesFactory.TransfersOutsideFromUs(
    "vdk mob pay mette norgaard ki",
    "Mette Nørgaard",
    ComparingType.Exact),

RulesFactory.TransfersOutsideFromUs(
    "vdk mob pay lone leth byriel",
    "Lone Leth",
    ComparingType.Exact),

RulesFactory.TransfersOutsideFromUs(
    "vdk mob pay dorthe moller joh",
    "Dorthe Møller",
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "dk surfline aps",
    "Surfline",
    Category.TelefonTvInternet,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "vdk dfp193453411",
    "DFP",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "vdk iexpert aps",
    "IExpert",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.ClothesAndShoes(
    "dk km mode risskov",
    "KM Mode",
    Category.Toej,
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "vdk squaretradecopay",
    "SquareTrade",
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "vdk bogshoppen",
    "Bogshoppen",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "dk isager",
    "Isager",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "vdk kontoret",
    "Kontoret",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "vdk inntq ab",
    "INNTQ",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "mobilepay tattoo fashion skive",
    "Tattoo Fashion",
    Category.OtherThanGroceries,
    ComparingType.Exact),

RulesFactory.GeneralShopping(
    "mobilepay old boys45",
    "Old Boys",
    Category.Fritid,
    ComparingType.Exact),

RulesFactory.InternalAccountTransfer(
    "overfoersel",
    "Overførsel",
    Category.Overfoersel,
    ComparingType.Exact),
        RulesFactory.GeneralShopping(
            "vdk stiftelsen idre",
            "Idre",
            Category.Ferie,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "vdk idre fja ll 18",
            "Idre Fjäll",
            Category.Ferie,
            ComparingType.Exact),
        // LIDL

        RulesFactory.EverydayGrocery(
            "dk lidl",
            "Lidl",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk lidlvericenter",
            "Lidl Veri Center",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk lidl veri center",
            "Lidl Veri Center",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk lidl221arhusskejby",
            "Lidl Skejby",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk lidl arhus n",
            "Lidl Aarhus N",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk lidl158skive",
            "Lidl Skive",
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk lidl210arhusrisskov",
            "Lidl Risskov",
            ComparingType.Exact),
        // FAMILY / TRANSFERS

        RulesFactory.TransfersFamilyToUs(
            "mobilepay eigil hvidberg johns",
            "Eigil",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay bent moeller joh",
            "Bent Møller",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay bent moeller johnsen",
            "Bent Møller",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay dorthe moeller j",
            "Dorthe Møller",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay dorthe moeller johnse",
            "Dorthe Møller",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay sonja john",
            "Sonja",
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
            "mobilepay julius hvi",
            "Julius",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay anne marie dahl",
            "Anne-Marie Dahl",
            ComparingType.Exact),
        // OTHER PEOPLE

        RulesFactory.TransfersOutsideToUs(
            "mobilepay claus michael l",
            "Claus Michael",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay jan isoee kjaer",
            "Jan Isøe Kjær",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay inge kjaerulff t",
            "Inge Kjærulff",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay inge kjaerulff torp",
            "Inge Kjærulff",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mette clemmense",
            "Mette Clemmensen",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mette clemmensen",
            "Mette Clemmensen",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay niels christen",
            "Niels",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay niels chri",
            "Niels",
            ComparingType.Exact),
        // SPECIALS

        RulesFactory.GeneralShopping(
            "dk safeticket dk",
            "Safeticket",
            Category.Fritid,
            ComparingType.Exact),

        RulesFactory.EverydayGrocery(
            "vdk reenberg gront",
            "Reenberg Grønt",
            ComparingType.Exact),
        RulesFactory.TransfersFamilyToUs(
            "vdk mob pay bertil hvidberg j",
            "Bertil",
            ComparingType.Exact),
        RulesFactory.GiftFromUs(
            "mobilepay tusind tak for din h",
            "Donation",
            ComparingType.Exact),
    ];
}