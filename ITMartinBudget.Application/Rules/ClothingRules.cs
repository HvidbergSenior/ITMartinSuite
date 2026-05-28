using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ClothingRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.ClothesAndShoes(
            "h m",
            "H&M",
            comparingType: ComparingType.Word),

        RulesFactory.ClothesAndShoes(
            "about you",
            "About You"),

        RulesFactory.ClothesAndShoes(
            "only stores",
            "Only"),

        RulesFactory.ClothesAndShoes(
            "skechers",
            "Skechers"),

        RulesFactory.ClothesAndShoes(
            "stm sport",
            "STM Sport"),

        RulesFactory.ClothesAndShoes(
            "paw sko",
            "Paw Sko"),

        RulesFactory.ClothesAndShoes(
            "shopping4net",
            "Shopping4Net"),

        RulesFactory.ClothesAndShoes(
            "ecco",
            "Ecco",
            comparingType: ComparingType.Word),

        RulesFactory.ClothesAndShoes(
            "2nddeluxe",
            "2ndDeluxe"),

        RulesFactory.ClothesAndShoes(
            "blue tomato",
            "Blue Tomato"),

        RulesFactory.ClothesAndShoes(
            "klarna",
            "Klarna",
            comparingType: ComparingType.Word),

        RulesFactory.ClothesAndShoes(
            "trendhim",
            "Trendhim"),

        RulesFactory.ClothesAndShoes(
            "rivalxt",
            "RivalXT"),

        RulesFactory.ClothesAndShoes(
            "reshopit",
            "Reshopit"),

        RulesFactory.ClothesAndShoes(
            "julie sandlau",
            "Julie Sandlau"),

        RulesFactory.ClothesAndShoes(
            "sportmaster",
            "Sportmaster"),

        RulesFactory.ClothesAndShoes(
            "modekompagniet",
            "Modekompagniet"),

        RulesFactory.ClothesAndShoes(
            "mft knitwear",
            "MFT Knitwear"),

        RulesFactory.ClothesAndShoes(
            "reshoppit",
            "Reshoppit"),

        RulesFactory.ClothesAndShoes(
            "bruuns galleri",
            "Bruuns Galleri"),

        RulesFactory.ClothesAndShoes(
            "hyldedeluxe",
            "HyldeDeluxe"),

        RulesFactory.ClothesAndShoes(
            "ideal of sweden",
            "Ideal of Sweden"),

        RulesFactory.ClothesAndShoes(
            "zalando",
            "Zalando"),

        RulesFactory.ClothesAndShoes(
            "vero moda",
            "Vero Moda"),

        RulesFactory.ClothesAndShoes(
            "jack and jones",
            "Jack & Jones"),

        RulesFactory.ClothesAndShoes(
            "butler loftet",
            "Butler Loftet")
    ];
}