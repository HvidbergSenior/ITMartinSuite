using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class UnknownRules
{
    public static readonly List<TransactionRule> Items =
    [
        // =====================================
        // Fixed / Health / Misc
        // =====================================

        Subscription(
            "fitnessunited",
            "Fitness United",
            Category.Subscription,
            ComparingType.Contains),

        PersonalCare(
            "norregades apot",
            "Apotek",
            Category.Sundhed,
            ComparingType.Contains),

        ClothesAndShoes(
            "kirkens korshaer",
            "Kirkens Korshær",
            Category.Toej,
            ComparingType.Contains),

        Subscription(
            "suno inc",
            "Suno",
            Category.Subscription,
            ComparingType.Contains),

        GeneralShopping(
            "stofshop abyhoj",
            "Stofshop",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "skive tek",
            "Skive Tek",
            Category.Fritid,
            ComparingType.Contains),

        GeneralShopping(
            "noddebutikken",
            "Nøddebutikken",
            Category.Hjem,
            ComparingType.Contains),

        ClothesAndShoes(
            "sikkerhedsudstyr",
            "Sikkerhedsudstyr",
            Category.Toej,
            ComparingType.Contains),

        GeneralShopping(
            "kjaer sommerfeldt",
            "Kjær Sommerfeldt",
            Category.Hjem,
            ComparingType.Contains),

        GiftFromUs(
            "hjemmet",
            "Hjemmet",
            ComparingType.Contains),

        GeneralShopping(
            "the way ahead group",
            "The Way Ahead Group",
            Category.Hjem,
            ComparingType.Contains),

        GeneralShopping(
            "drandersvej",
            "Drandersvej",
            Category.Hjem,
            ComparingType.Contains),

        FixedExpense(
            "til alka",
            "Alka",
            Category.Forsikring,
            ComparingType.Contains),

        Refund(
            "sygeforsikringendanmark",
            "Danmark Refund",
            ComparingType.Contains),

        // =====================================
        // Grocery
        // =====================================

        RestaurantCafe(
            "bagergaarden hinnerup",
            "Bagergaarden",
            Category.Cafe,
            ComparingType.Contains),

        EverydayGrocery(
            "mbmadmarked",
            "MB Madmarked",
            ComparingType.Contains),

        EverydayGrocery(
            "reenberg groent",
            "Reenberg Grønt",
            ComparingType.Contains),

        // =====================================
        // Restaurant / Cafe
        // =====================================

        RestaurantCafe(
            "ruth co",
            "Ruth & Co",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "ruth",
            "Ruth & Co",
            Category.Restaurant,
            ComparingType.Word),

        RestaurantCafe(
            "viva italy",
            "Viva Italy",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "viva italy brunch",
            "Viva Italy",
            Category.Restaurant,
            ComparingType.Contains),

        RestaurantCafe(
            "mackies",
            "Mackies",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "wok shop",
            "Wok Shop",
            Category.Takeaway,
            ComparingType.Contains),

        RestaurantCafe(
            "lunch",
            "Lunch",
            Category.Restaurant,
            ComparingType.Word),

        Entertainment(
            "zettle escapist",
            "Escapist",
            Category.Fritid,
            ComparingType.Contains),

        RestaurantCafe(
            "walthers musikcafe",
            "Walthers",
            Category.Cafe,
            ComparingType.Contains),

        // =====================================
        // Parking / Entertainment / Apps
        // =====================================

        Entertainment(
            "gallafest",
            "Gallafest",
            Category.Fritid,
            ComparingType.Contains),

        Subscription(
            "joytunes",
            "JoyTunes",
            Category.Subscription,
            ComparingType.Contains),

        Subscription(
            "google play",
            "Google Play",
            Category.Subscription,
            ComparingType.Contains),

        Entertainment(
            "myticket",
            "MyTicket",
            Category.KoncertBio,
            ComparingType.Contains),

        Entertainment(
            "fastelavsbazar",
            "Fastelavsbazar",
            Category.Fritid,
            ComparingType.Contains),

        GiftFromUs(
            "boernecancerfonden",
            "Børnecancerfonden",
            ComparingType.Contains),

        GiftFromUs(
            "bla kors",
            "Blå Kors",
            ComparingType.Contains),

        // =====================================
        // Generic MobilePay
        // =====================================

        TransfersFamilyToUs(
            "dk mobilepay",
            "MobilePay Generic",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "1000takfordinhj",
            "Gift / Transfer",
            ComparingType.Contains),

        // =====================================
        // Family / Friends Transfers
        // =====================================

        TransfersFamilyToUs(
            "jan isoe",
            "Jan Isøe",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "jan isoe",
            "Jan Isøe",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "mille ea bastholm",
            "Mille Ea Bastholm",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "mille ea bastholm",
            "Mille Ea Bastholm",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "mathilde rask",
            "Mathilde Rask",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "mathilde rask",
            "Mathilde Rask",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "mathias olin hvidber",
            "Mathias Olin Hvidberg",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "mathias olin hvidber",
            "Mathias Olin Hvidberg",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "ida koester",
            "Ida Køster",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "ida koester",
            "Ida Køster",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "sarah lahn",
            "Sarah Lahn",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "sarah lahn",
            "Sarah Lahn",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "michael guldham",
            "Michael Guldham",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "michael guldham",
            "Michael Guldham",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "maja olesen",
            "Maja Olesen",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "maja olesen",
            "Maja Olesen",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "julie rose rahb",
            "Julie Rose Rahb",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "julie rose rahb",
            "Julie Rose Rahb",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "emma stoehr",
            "Emma Støhr",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "emma stoehr",
            "Emma Støhr",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "karsten juel bu",
            "Karsten Juel Bunch",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "karsten juel bu",
            "Karsten Juel Bunch",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "dorthe donbaek ebbese",
            "Dorthe Donbæk Ebbese",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "dorthe donbaek ebbese",
            "Dorthe Donbæk Ebbese",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "christian birkefeldt",
            "Christian Birkefeldt",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "christian birkefeldt",
            "Christian Birkefeldt",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "alexander kure",
            "Alexander Kure",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "alexander kure",
            "Alexander Kure",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "claus michael",
            "Claus Michael",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "claus michael",
            "Claus Michael",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "charlotte frimuth",
            "Charlotte Frimuth",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "charlotte frimuth",
            "Charlotte Frimuth",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "anne marie dahl",
            "Anne-Marie Dahl",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "anne marie dahl",
            "Anne-Marie Dahl",
            ComparingType.Contains),

        TransfersFamilyToUs(
            "dorthe moeller",
            "Dorthe Møller",
            ComparingType.Contains),

        TransfersFamilyFromUs(
            "dorthe moeller",
            "Dorthe Møller",
            ComparingType.Contains)
    ];
}