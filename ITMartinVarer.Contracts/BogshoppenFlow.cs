namespace ITMartinVarer.Contracts;

/// <summary>
/// Bogshoppen's way from a thing in a box to a sale (user 2026-11-05: "1. Add products -> find prices -> storage ->
/// on bogshoppen/produkter -> sale"). One responsibility per helper; this list is only names + public addresses, so the
/// helpers can point to each other without knowing each other's insides.
/// </summary>
public static class BogshoppenFlow
{
    public const int Vareinfo = 1, Priser = 2, Lager = 3, Produkter = 4, Salg = 5;

    public static readonly IReadOnlyList<(string Name, string Url)> Steps =
    [
        ("Tilføj varer", "https://vareinfo.bogshoppen.dk/"),
        ("Find priser", "https://vurdering.bogshoppen.dk/"),   // Vurdering (route live 2026-11-06)
        ("Læg på lager", "https://lager.bogshoppen.dk/kasser"),
        ("På bogshoppen.dk", "https://bogshoppen.dk/produkter"),
        ("Sælg", "https://salg.bogshoppen.dk/"),
    ];
}
