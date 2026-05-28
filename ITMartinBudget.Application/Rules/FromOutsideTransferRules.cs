using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FromOutsideTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.TransfersOutsideToUs(
            "mobilepay marianne hvidberg",
            "Marianne",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay dorthe moeller",
            "Dorthe",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay bent moeller",
            "Bent",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay sonja john",
            "Sonja",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay marianne hvidberg",
            "Marianne",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mike ea basho",
            "Mike",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay inge kjaerulff",
            "Inge",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay jan isoe kjaer",
            "Jan",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay laura fogdal",
            "Laura",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay anne bro friis",
            "Anne Bro Friis",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay sonja johnsen",
            "Sonja",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay dorthe donsaek ebbese",
            "Dorthe",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "jan isoe kjaer",
            "Jan Isøe Kjær",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay mette toft vestbjerg",
            "Mette",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay karsten juul bunch",
            "Karsten",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay kristian hertoft",
            "Kristian",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "frida kjelsmark",
            "Frida",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "frida kjelsmark",
            "Frida",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "inge kjaerulff",
            "Inge",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "inge kjaerulff",
            "Inge",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "anne bro friis",
            "Anne Bro Friis",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "anne bro friis",
            "Anne Bro Friis",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mille ea bastho",
            "Mille Ea Bastho",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "emma staehr",
            "Emma Stæhr",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "michael guldham",
            "Michael Guldham",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mathias olin hvidber",
            "Mathias Olin",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "dorthe donbaek ebbese",
            "Dorthe Donbæk",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "siri alice birkefeld",
            "Siri Alice",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mette clemmensen",
            "Mette Clemmensen",
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "kongsvingervej",
            "Kongsvingervej",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "kongsvingervej1",
            "Kongsvingervej",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.Refund(
            "danmark",
            "Danmark Refund",
            ComparingType.Word),

        RulesFactory.GeneralShopping(
            "reenberg groent",
            "Reenberg Grønt",
            Category.Dagligvarer,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "hog fodbold",
            "HOG Fodbold",
            Category.Fritid,
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "bent moller johnsen",
            "Bent Møller Johnsen",
            ComparingType.Contains),

        RulesFactory.GeneralShopping(
            "mib madmarked",
            "MIB Madmarked",
            Category.Dagligvarer,
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "alex sner gunnarsson",
            "Alex Sner Gunnarsson",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "bla kors genbrug",
            "Blå Kors Genbrug",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "dorthe mol",
            "Dorthe",
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "fastelavnsbazar",
            "Fastelavnsbazar",
            Category.Fritid,
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "bent moller johnsen",
            "Bent Møller Johnsen",
            ComparingType.Contains),

        

        RulesFactory.TransfersOutsideToUs(
            "niels chri",
            "Niels",
            ComparingType.Contains),

        RulesFactory.GiftToUs(
            "rip rap og rup",
            "Rip Rap og Rup",
            ComparingType.Contains),

        RulesFactory.GiftToUs(
            "til tant og fjas m",
            "Tante og Fjas",
            ComparingType.Contains),

        RulesFactory.GeneralShopping(
            "safeticket dk",
            "SafeTicket",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.GeneralShopping(
            "kop og kande",
            "Kop & Kande",
            Category.Hjem,
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "aarhus kommune anke",
            "Aarhus Kommune",
            ComparingType.Contains)
    ];
}