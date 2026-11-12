namespace ITMartinHjem.Domain;

/// <summary>"Nyt" on the front page (user 2026-11-12: "Also interesting news -> MY LOF evenings"): Martin's own evening
/// course at LOF Aarhus, agreed with LOF by mail 12.11 (dates + room booked). Hammel (Thursdays?) and a Viborg workshop
/// are not settled yet - add them here when they are. Shown until the last evening has passed.</summary>
public static class News
{
    public const string Page = "/lof-aftener";

    public sealed record Course(string Title, string Teaser, string Place, string Time, IReadOnlyList<DateOnly> Dates, string Href);

    public static readonly Course LofAi = new(
        "Kunstig intelligens for begyndere – lær at hoppe, før du springer",
        "Mit aftenhold hos LOF Aarhus. Vi tager det roligt, og hver aften laver du din egen lille hjælper – fx en elplan, en budgethjælper eller en fakta-eller-fup-hjælper. Ingen forkundskaber.",
        "Fjordsgade forenings- og fritidshus, Aarhus",
        "Tirsdage kl. 19.00–21.15",
        [new(2027, 2, 2), new(2027, 2, 9), new(2027, 2, 23), new(2027, 3, 2), new(2027, 3, 9)],
        "https://lof.dk/aarhus");

    /// <summary>One LOF evening and its red thread (user 2026-11-12: "make connection from all my apps to the LOF areas -> to my
    /// facebook forlade/snap -> doomscrolling -> datacenter"): what we make, why, the snebolde it uses and Martin's own pages on it.
    /// Texts from Martin's own course text to LOF (mail 11.11). Only live apps + live itmartin.dk pages.</summary>
    public sealed record Evening(int Number, string Title, string Make, string Why,
        IReadOnlyList<(string Name, string Href)> Snebolde, IReadOnlyList<(string Name, string Href)> Read);

    public static readonly IReadOnlyList<Evening> LofEvenings =
    [
        new(1, "Elpriser og elbiler", "Familiens egen elplan: hvornår er strømmen billigst og grønnest?",
            "Du sparer penge – og når mange flytter forbruget, bruger vi mere vind og sol.",
            [("ElPriser", "https://elpriser.itmartin.dk/om"), ("Temperatur", "https://temperatur.itmartin.dk/om")],
            [("Grønt og bæredygtigt", "/baeredygtighed")]),
        new(2, "Udtryk – ord, taler og sange", "En hjælper, der hjælper dig med at sige det, du gerne vil sige.",
            "AI kan give flere en stemme – fx ordblinde og dem, der har svært ved at formulere sig.",
            [("Snak", "https://snak.itmartin.dk/om"), ("Stem", "https://stem.itmartin.dk/om")],
            [("AI og tænkning – mine pointer", "/ai-og-taenkning")]),
        new(3, "Penge og budget", "En enkel budgethjælper til familien.",
            "Overblik over, hvad pengene går til – og abonnementer, du havde glemt, der bare kører videre.",
            [("Gratis budget", "/budget"), ("Husk", "https://husk.itmartin.dk/om")],
            [("Ingen reklamer", "/ingen-reklamer")]),
        new(4, "Billeder og erindringer", "Et digitalt album til dine minder – dine billeder hjem fra Facebook, Instagram og Snapchat.",
            "Minder skal være til at finde igen – ikke ligge hos de store, uden dato, i digitalt rod.",
            [("Se et eksempel", "https://gallery.itmartin.dk/facebook-demo"), ("Billeder og film hjem", "/billeder-fra-skyen")],
            [("Derfor forlader jeg Facebook", "/farvel-facebook")]),
        new(5, "Fakta, nyheder og manipulation", "Din egen \"fakta eller fup\"-hjælper.",
            "Uendelige strømme holder os fast (doomscrolling), og en halv times scrolling bruger lige så meget strøm i datacentrene som ca. 200 korte AI-spørgsmål. Ryd op, før du lægger til – og brug kun AI, hvor den gør en forskel.",
            [],
            [("Derfor forlader jeg Facebook", "/farvel-facebook"), ("AI og tænkning", "/ai-og-taenkning"), ("Ingen reklamer", "/ingen-reklamer"), ("Grønt og bæredygtigt", "/baeredygtighed")]),
    ];

    /// <summary>Show the course until its last evening is over.</summary>
    public static bool Show(Course c, DateOnly today) => today <= c.Dates[^1];

    public static string DateLine(Course c) =>
        string.Join(", ", c.Dates.Select(d => $"{d.Day}.{d.Month}")) + $" {c.Dates[^1].Year}";
}
