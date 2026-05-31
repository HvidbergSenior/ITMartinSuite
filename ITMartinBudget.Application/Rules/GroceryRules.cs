using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class GroceryRules
{
    public static readonly List<TransactionRule> Items =
    [
        EverydayGrocery(
            "netto",
            "Netto",
            ComparingType.Word),

        EverydayGrocery(
            "rema",
            "Rema 1000",
            ComparingType.Contains),

        EverydayGrocery(
            "foetex",
            "Føtex",
            ComparingType.Contains),

        EverydayGrocery(
            "bilka",
            "Bilka",
            ComparingType.Word),

        EverydayGrocery(
            "vdk lidl",
            "Lidl",
            ComparingType.Exact),

        EverydayGrocery(
            "kvickly",
            "Kvickly",
            ComparingType.Word),

        EverydayGrocery(
            "365discount",
            "365discount",
            ComparingType.Contains),

        EverydayGrocery(
            "coop365",
            "Coop 365",
            ComparingType.Contains),

        EverydayGrocery(
            "dagli brugsen",
            "Dagli'Brugsen",
            ComparingType.Contains),

        EverydayGrocery(
            "superbrugsen",
            "SuperBrugsen",
            ComparingType.Contains),

        EverydayGrocery(
            "superbrugs",
            "SuperBrugsen",
            ComparingType.Contains),

        EverydayGrocery(
            "coop sb",
            "Coop SuperBrugsen",
            ComparingType.Contains),

        EverydayGrocery(
            "meny",
            "Meny",
            ComparingType.Word),

        EverydayGrocery(
            "vdk spar",
            "SPAR",
            ComparingType.Contains),

        EverydayGrocery(
            "min koebmand",
            "Min Købmand",
            ComparingType.Contains),

        EverydayGrocery(
            "reenberg groent",
            "Reenberg Grønt",
            ComparingType.Contains),

        EverydayGrocery(
            "loevbjerg",
            "Løvbjerg",
            ComparingType.Contains),

        EverydayGrocery(
            "tgtg",
            "Too Good To Go",
            ComparingType.Word),

        EverydayGrocery(
            "too good to go",
            "Too Good To Go",
            ComparingType.Contains),

        EverydayGrocery(
            "bla kors genbrug",
            "Blå Kors Genbrug",
            ComparingType.Contains),

        EverydayGrocery(
            "mibmadmarked",
            "MIB Madmarked",
            ComparingType.Contains),

        EverydayGrocery(
            "daglibrugsen",
            "DagliBrugsen",
            ComparingType.Contains),
        EverydayGrocery(
            "mbmadmarked",
            "MB Madmarked",
            ComparingType.Contains)
    ];
}