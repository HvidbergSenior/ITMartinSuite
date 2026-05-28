using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class UnknownRules
{
    public static readonly List<TransactionRule> Items =
    [
       // Fitness

FixedExpense(
    "fitnessunited",
    "Fitness United",
    Category.Subscription),

// Pharmacy / health

PersonalCare(
    "norregades apot",
    "Apotek",
    Category.Sundhed),

ThingsOtherThanClothes(
    "kirkens korshaer",
    "Kirkens Korshær",
    Category.Toej),

TransfersFromOutsideReceived(
    "jan isoe",
    "Jan Isøe"),

TransfersToOutsideGiven(
    "jan isoe",
    "Jan Isøe"),

TransfersFromOutsideReceived(
    "mille ea bastholm",
    "Mille Ea Bastholm"),

TransfersToOutsideGiven(
    "mille ea bastholm",
    "Mille Ea Bastholm"),

TransfersFromOutsideReceived(
    "mathilde rask",
    "Mathilde Rask"),

TransfersToOutsideGiven(
    "mathilde rask",
    "Mathilde Rask"),
TransfersFromOutsideReceived(
    "mathias olin hvidber",
    "Mathias Olin Hvidberg"),

TransfersToOutsideGiven(
    "mathias olin hvidber",
    "Mathias Olin Hvidberg"),
TransfersFromOutsideReceived(
    "ida koester",
    "Ida Køster"),

TransfersToOutsideGiven(
    "ida koester",
    "Ida Køster"),

TransfersFromOutsideReceived(
    "sarah lahn",
    "Sarah Lahn"),

TransfersToOutsideGiven(
    "sarah lahn",
    "Sarah Lahn"),

TransfersFromOutsideReceived(
    "michael guldham",
    "Michael Guldham"),

TransfersToOutsideGiven(
    "michael guldham",
    "Michael Guldham"),

TransfersFromOutsideReceived(
    "maja olesen",
    "Maja Olesen"),

TransfersToOutsideGiven(
    "maja olesen",
    "Maja Olesen"),

TransfersFromOutsideReceived(
    "julie rose rahb",
    "Julie Rose Rahb"),

TransfersToOutsideGiven(
    "julie rose rahb",
    "Julie Rose Rahb"),

TransfersFromOutsideReceived(
    "emma stoehr",
    "Emma Støhr"),
TransfersFromOutsideReceived(
    "karsten juel bu",
    "Karsten Juel Bunch"),

TransfersToOutsideGiven(
    "karsten juel bu",
    "Karsten Juel Bunch"),
TransfersToOutsideGiven(
    "emma stoehr",
    "Emma Støhr"),
TransfersFromOutsideReceived(
    "dorthe donbaek ebbese",
    "Dorthe Donbæk Ebbese"),

TransfersToOutsideGiven(
    "dorthe donbaek ebbese",
    "Dorthe Donbæk Ebbese"),
TransfersFromOutsideReceived(
    "christian birkefeldt",
    "Christian Birkefeldt"),

TransfersToOutsideGiven(
    "christian birkefeldt",
    "Christian Birkefeldt"),

TransfersFromOutsideReceived(
    "alexander kure",
    "Alexander Kure"),

TransfersToOutsideGiven(
    "alexander kure",
    "Alexander Kure"),

TransfersFromOutsideReceived(
    "claus michael",
    "Claus Michael"),

TransfersToOutsideGiven(
    "claus michael",
    "Claus Michael"),

TransfersFromOutsideReceived(
    "charlotte frimuth",
    "Charlotte Frimuth"),

TransfersToOutsideGiven(
    "charlotte frimuth",
    "Charlotte Frimuth"),

TransfersFromOutsideReceived(
    "anne marie dahl",
    "Anne-Marie Dahl"),

TransfersToOutsideGiven(
    "anne marie dahl",
    "Anne-Marie Dahl"),

TransfersFromOutsideReceived(
    "dorthe moeller",
    "Dorthe Møller"),

TransfersToOutsideGiven(
    "dorthe moeller",
    "Dorthe Møller"),

// Misc

FixedExpense(
    "suno inc",
    "Suno", Category.Subscription),

ThingsOtherThanClothes(
    "stofshop abyhoj",
    "Stofshop",
    Category.Bolig),

RestaurantCafe(
    "ruth co",
    "Ruth Co",
    Category.Restaurant),

ThingsOtherThanClothes(
    "skive tek",
    "Skive Tek",
    Category.Fritid),
       


ThingsOtherThanClothes(
    "ruth co",
    "Ruth & Co", Category.OtherThanGroceries),

ThingsOtherThanClothes(
    "noddebutikken",
    "Nøddebutikken", Category.OtherThanGroceries),

ThingsOtherThanClothes(
    "sikkerhedsudstyr",
    "Sikkerhedsudstyr", Category.OtherThanGroceries),

ThingsOtherThanClothes(
    "kjaer sommerfeldt",
    "Kjær Sommerfeldt", Category.OtherThanGroceries),
Refund(
    "danmark",
    "Danmark Refund"),

RestaurantCafe(
    "ruth",
    "Ruth & Co",
    Category.Restaurant),

TransfersFromOutsideReceived(
    "siri alice birkefeld",
    "Siri Alice Birkefeld"),

TransfersToOutsideGiven(
    "siri alice birkefeld",
    "Siri Alice Birkefeld"),

TransfersFromOutsideReceived(
    "mette clemmensen",
    "Mette Clemmensen"),

TransfersToOutsideGiven(
    "mette clemmensen",
    "Mette Clemmensen"),
    ];
}