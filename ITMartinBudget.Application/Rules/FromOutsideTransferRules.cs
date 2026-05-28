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
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay dorthe moeller",
            "Dorthe",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay bent moeller",
            "Bent",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay sonja john",
            "Sonja",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mobilepay marianne hvidberg",
            "Marianne",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay mike ea basho",
            "Mike",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay inge kjaerulff",
            "Inge",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay jan isoe kjaer",
            "Jan",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay laura fogdal",
            "Laura",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay anne bro friis",
            "Anne Bro Friis",
            ComparingType.Contains),

        TransfersOutsideFromUs(
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

        RulesFactory.GeneralShopping(
            "reenberg groent",
            "Reenberg Grønt",
            Category.Dagligvarer,
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
            ComparingType.Contains),
          TransfersOutsideToUs(
            "jan isoe",
            "Jan Isøe",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "jan isoe",
            "Jan Isøe",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mille ea bastholm",
            "Mille Ea Bastholm",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mille ea bastholm",
            "Mille Ea Bastholm",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mathilde rask",
            "Mathilde Rask",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mathilde rask",
            "Mathilde Rask",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mathias olin hvidber",
            "Mathias Olin Hvidberg",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mathias olin hvidber",
            "Mathias Olin Hvidberg",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "ida koester",
            "Ida Køster",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "ida koester",
            "Ida Køster",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "sarah lahn",
            "Sarah Lahn",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "sarah lahn",
            "Sarah Lahn",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "michael guldham",
            "Michael Guldham",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "michael guldham",
            "Michael Guldham",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "maja olesen",
            "Maja Olesen",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "maja olesen",
            "Maja Olesen",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "julie rose rahb",
            "Julie Rose Rahb",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "julie rose rahb",
            "Julie Rose Rahb",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "emma stoehr",
            "Emma Støhr",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "emma stoehr",
            "Emma Støhr",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "karsten juel bu",
            "Karsten Juel Bunch",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "karsten juel bu",
            "Karsten Juel Bunch",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "dorthe donbaek ebbese",
            "Dorthe Donbæk Ebbese",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "dorthe donbaek ebbese",
            "Dorthe Donbæk Ebbese",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "christian birkefeldt",
            "Christian Birkefeldt",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "christian birkefeldt",
            "Christian Birkefeldt",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "alexander kure",
            "Alexander Kure",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "alexander kure",
            "Alexander Kure",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "claus michael",
            "Claus Michael",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "claus michael",
            "Claus Michael",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "charlotte frimuth",
            "Charlotte Frimuth",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "charlotte frimuth",
            "Charlotte Frimuth",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "anne marie dahl",
            "Anne-Marie Dahl",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "anne marie dahl",
            "Anne-Marie Dahl",
            ComparingType.Contains),

        TransfersOutsideToUs(
            "mobilepay bent moeller",
            "Bent Møller",
            ComparingType.Contains),
        TransfersOutsideFromUs(
            "mobilepay alex snaer gunnarsson",
            "Alex Snær Gunnarsson",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mobilepay mathilde caroline",
            "Mathilde-Caroline",
            ComparingType.Contains),

   
        TransfersOutsideToUs(
            "mobilepay thomas fug",
            "Thomas Fug",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mobilepay trine marie vinkler",
            "Trine Marie Vinkler",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mobilepay malene hvidberg",
            "Malene Hvidberg",
            ComparingType.Contains),
        
        TransfersOutsideToUs(
            "mobilepay tobias norman jensen",
            "Tobias Norman Jensen",
            ComparingType.Contains),
        TransfersOutsideToUs(
            "mobilepay bent moeller johnsen",
            "Bent Møller Johnsen",
            ComparingType.Contains),
        RulesFactory.TransfersOutsideFromUs(
            "mobilepay bent moeller johnsen",
            "Bent Møller",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay dorthe moel",
            "Dorthe Møller",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay dorthe moeller johnse",
            "Dorthe Møller",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay tobias norman jensen",
            "Tobias Norman Jensen",
            ComparingType.Contains),
    ];
}