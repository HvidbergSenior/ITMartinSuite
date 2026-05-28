using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ClothingRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.ClothesAndShoes(
            "h&m",
            "H&M",
            Category.Toej,
            ComparingType.Word),

        RulesFactory.ClothesAndShoes(
            "about you",
            "About You",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "only stores",
            "Only",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "skechers",
            "Skechers",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "stm sport",
            "STM Sport",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "paw sko",
            "Paw Sko",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "shopping4net",
            "Shopping4Net",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "ecco",
            "Ecco",
            Category.Toej,
            ComparingType.Word),

        RulesFactory.ClothesAndShoes(
            "2nddeluxe",
            "2ndDeluxe",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "blue tomato",
            "Blue Tomato",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "klarna",
            "Klarna",
            Category.Toej,
            ComparingType.Word),

        RulesFactory.ClothesAndShoes(
            "trendhim",
            "Trendhim",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "rivalxt",
            "RivalXT",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "reshopit",
            "Reshopit",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "julie sandlau",
            "Julie Sandlau",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "sportmaster",
            "Sportmaster",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "modekompagniet",
            "Modekompagniet",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "mft knitwear",
            "MFT Knitwear",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "reshoppit",
            "Reshoppit",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "bruuns galleri",
            "Bruuns Galleri",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "hyldedeluxe",
            "HyldeDeluxe",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "ideal of sweden",
            "Ideal of Sweden",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "zalando",
            "Zalando",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "vero moda",
            "Vero Moda",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "jack and jones",
            "Jack & Jones",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.ClothesAndShoes(
            "butler loftet",
            "Butler Loftet",
            Category.Toej,
            ComparingType.Contains),
        RulesFactory.ClothesAndShoes(
            "sikkerhedsudstyr",
            "Sikkerhedsudstyr",
            Category.Toej,
            ComparingType.Contains),
        RulesFactory.ClothesAndShoes(
            "kirkens korshaer",
            "Kirkens Korshær",
            Category.Toej,
            ComparingType.Contains),

    ];
}