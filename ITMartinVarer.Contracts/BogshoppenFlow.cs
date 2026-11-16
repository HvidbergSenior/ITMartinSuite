namespace ITMartinVarer.Contracts;

/// <summary>
/// Bogshoppen's way from a thing in a box to a sale (user 2026-11-05: "1. Add products -> find prices -> storage ->
/// on bogshoppen/produkter -> sale"). One responsibility per helper; this list is only names + public addresses, so the
/// helpers can point to each other without knowing each other's insides.
/// </summary>
public static class BogshoppenFlow
{
    // 2026-11-10 ("I want it to reflect that it is the same apps"): shown as tabs, one per app, in the order the work goes now -
    // pictures, then (by themselves) stock and prices, then the sale; bogshoppen.dk last.
    public const int Vareinfo = 1, Lager = 2, Priser = 3, Salg = 4, Produkter = 5;

    public static readonly IReadOnlyList<(string Name, string Url)> Steps =
    [
        ("Tag billeder", "https://vareinfo.bogshoppen.dk/"),
        ("Lager", "https://lager.bogshoppen.dk/"),
        ("Priser", "https://vurdering.bogshoppen.dk/"),
        ("Salg", "https://salg.bogshoppen.dk/"),
        ("bogshoppen.dk", "https://bogshoppen.dk/produkter"),
    ];
}
