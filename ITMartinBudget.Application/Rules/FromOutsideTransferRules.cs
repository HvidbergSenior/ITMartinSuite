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
            "Dorthe Søster",
            ComparingType.Exact),
        
        RulesFactory.TransfersOutsideToUs(
            "mobilepay marianne hvidbe",
            "Marianne",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "jan isoe kjaer",
            "Jan Isøe Kjær",
            ComparingType.Contains),

        TransfersOutsideFromUs(
            "mobilepay alex snaer gunnarsson",
            "Alex Snær Gunnarsson",
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
            "mobilepay karsten juel bu",
            "Karsten Bunch",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay mille ea bastho",
            "Mille Bastholm",
            ComparingType.Exact),

        TransfersOutsideToUs(
            "mobilepay mille ea bastholm",
            "Mille Bastholm",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay mathilde caroline",
            "Mathilde Caroline",
            ComparingType.Exact),
   
        TransfersOutsideToUs(
            "mobilepay thomas fug",
            "Thomas Fug",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay trine marie vinkler",
            "Trine Vinkler",
            ComparingType.Exact),

        TransfersOutsideFromUs(
            "mobilepay malene hvidberg",
            "Malene",
            ComparingType.Exact),
        
        TransfersOutsideToUs(
            "mobilepay tobias norman jensen",
            "Tobias Norman",
            ComparingType.Exact),
        RulesFactory.TransfersOutsideToUs(
            "mobilepay mike ea basho",
            "Mike",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay ida koster hei",
            "Ida Køster",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mathilde rass k",
            "Mathild Rass",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay laura fogdal li",
            "Laura Fogdal",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay frida kjedsmark",
            "Frida Kjedsmark",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay inge kjaerulf t",
            "Inge Kjærulf",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay karl jon nielsen",
            "Karl Jon Nielsen",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay mette toft vestbjerg",
            "Mette Vestbjerg",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay karsten juel bunch",
            "Karsten Bunch",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay arne bro friis jense",
            "Arne Bro Friis",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay ida koster hebos",
            "Ida Køster",
            ComparingType.Contains),

    
        RulesFactory.TransfersOutsideFromUs(
            "marie elizabeth",
            "Marie Elizabeth",
            ComparingType.Contains),
        
        RulesFactory.TransfersOutsideToUs(
    "mobilepay allan carl erik",
    "Allan",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay andreas benjamin gjoe",
    "Andreas Benjamin",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay anne marie dahl",
    "Anne-Marie Dahl",
    ComparingType.Exact),


RulesFactory.TransfersOutsideToUs(
    "mobilepay claus michael l",
    "Claus Michael",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay clara rindom hoe",
    "Clara Rindom",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorthe don",
    "Dorthe Donbæk",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorthe moeller j",
    "Dorthe Søster",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorthe moeller johnse",
    "Dorthe Søster",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorte rindom je",
    "Dorte Rindom",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay dorte rindom jeppese",
    "Dorte Rindom",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay eik nysom boeg j",
    "Eik",
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
    "mobilepay jan isoee kjaer",
    "Jan Isøe Kjær",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay jesper mahler clemme",
    "Jesper Clemmensen",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay liikke visby bunede",
    "Rikke Visby",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay lisbeth kr",
    "Lisbeth Krogshede",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay lisbeth krogshe",
    "Lisbeth Krogshede",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay mads gjellerod",
    "Mads Gjellerod",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay mathias olin hv",
    "Mathias",
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
    "mobilepay mona olin hvidb",
    "Mona",
    ComparingType.Contains),

RulesFactory.TransfersOutsideToUs(
    "mobilepay mona olin hvidberg",
    "Mona",
    ComparingType.Contains),

RulesFactory.TransfersOutsideToUs(
    "mobilepay niels chri",
    "Niels",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay niels christen",
    "Niels",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay per nielsen",
    "Per Nielsen",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay peter lau torst niel",
    "Peter Nielsen",
    ComparingType.Exact),

RulesFactory.TransfersOutsideToUs(
    "mobilepay rikke visby bunch",
    "Rikke Visby",
    ComparingType.Exact),


RulesFactory.TransfersOutsideToUs(
    "vdk squaretradecopay",
    "SquareTrade",
    ComparingType.Exact),

TransfersOutsideToUs(
    "mobblaa korsense",
    "Mette Clemmense",
    ComparingType.Exact),
        RulesFactory.TransfersOutsideFromUs(
            "mobilepay malene hvi",
            "Malene",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "vdk mob pay christian birkefe",
            "Christian",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "vdk mob pay christina aitken",
            "Christina",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "vdk mob pay dorthe moller joh",
            "Dorthe Søster",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "vdk mob pay lone leth byriel",
            "Lone Leth",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "vdk mob pay mette norgaard ki",
            "Mette Nørgaard",
            ComparingType.Exact),
        RulesFactory.TransfersOutsideToUs(
            "mobilepay 1 000 tak for din hj",
            "Donation",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay blaa kors genbrug a",
            "Blå Kors",
            ComparingType.Exact),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay tusind tak for din h",
            "Donation",
            ComparingType.Exact),
    ];
}