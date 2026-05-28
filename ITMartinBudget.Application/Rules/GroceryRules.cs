using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Rules;

public static class GroceryRules
{
    public static readonly List<TransactionRule> Items =
    [
        EverydayGrocery(
            "netto",
            "Netto"),

        EverydayGrocery(
            "rema",
            "Rema 1000"),

        EverydayGrocery(
            "foetex",
            "Føtex"),

        EverydayGrocery(
            "bilka",
            "Bilka"),

        EverydayGrocery(
            "lidl",
            "Lidl"),

        EverydayGrocery(
            "kvickly",
            "Kvickly"),

        EverydayGrocery(
            "365discount",
            "365discount"),

        EverydayGrocery(
            "coop365",
            "Coop 365"),

        EverydayGrocery(
            "dagli brugsen",
            "Dagli'Brugsen"),

        EverydayGrocery(
            "superbrugsen",
            "SuperBrugsen"),

        EverydayGrocery(
            "superbrugs",
            "SuperBrugsen"),

        EverydayGrocery(
            "coop sb",
            "Coop SuperBrugsen"),

        EverydayGrocery(
            "meny",
            "Meny"),

        EverydayGrocery(
            "spar",
            "SPAR"),

        EverydayGrocery(
            "min koebmand",
            "Min Købmand"),

        EverydayGrocery(
            "reenberg groent",
            "Reenberg Grønt"),

        EverydayGrocery(
            "loevbjerg",
            "Løvbjerg"),

        EverydayGrocery(
            "tgtg",
            "Too Good To Go"),

        EverydayGrocery(
            "too good to go",
            "Too Good To Go")
    ];
}