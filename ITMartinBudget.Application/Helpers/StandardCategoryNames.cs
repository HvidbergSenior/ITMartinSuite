namespace ITMartinBudget.Application.Helpers;

// Always-offered starter categories, requested explicitly so a brand-new
// ledger's category picker isn't empty until enough ad-hoc names accumulate
// from real transactions. Purely suggestions in the datalist - a category
// only actually exists once a CategoryRule references it.
public static class StandardCategoryNames
{
    public static readonly IReadOnlyList<string> Names =
    [
        "Abonnementer",
        "Løn",
        "Bolig",
        "Bil",
        "Husleje",
        "Dagligvarer",
        "Forsikring",
        "Benzin",
    ];
}
