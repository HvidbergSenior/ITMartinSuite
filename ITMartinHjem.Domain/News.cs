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
    public sealed record Evening(int Number, string Title, string Make, string More, string Why,
        IReadOnlyList<(string Name, string Href)> Snebolde, IReadOnlyList<(string Name, string Href)> Read);

    public static readonly IReadOnlyList<Evening> LofEvenings =
    [
        new(1, "Elpriser og elbiler", "Lav familiens egen elplan.",
            "Hvornår er strømmen billigst og grønnest? Hvornår giver det bedst mening at vaske tøj, lade bilen eller lave mad?",
            "Du kan spare penge – og når mange flytter deres forbrug, kan vi udnytte mere vind- og solenergi.",
            [("ElPriser", "https://elpriser.itmartin.dk/om"), ("Temperatur", "https://temperatur.itmartin.dk/om")],
            [("Grønt og bæredygtigt", "/baeredygtighed")]),
        new(2, "Udtryk – ord, taler og sange", "Lav en hjælper, der hjælper dig med at sige det, du gerne vil sige.",
            "Skriv digte, taler, sange eller tekster – også hvis det normalt er svært at få ordene ned på papir.",
            "AI kan give flere mennesker en stemme, blandt andet ordblinde og mennesker, der har svært ved at formulere sig.",
            [("Snak", "https://snak.itmartin.dk/om"), ("Stem", "https://stem.itmartin.dk/om")],
            [("AI og tænkning – mine pointer", "/ai-og-taenkning")]),
        new(3, "Penge og budget", "Lav en enkel budgethjælper til familien.",
            "Få overblik over, hvad pengene egentlig går til – og opdag måske abonnementer eller udgifter, I havde glemt.",
            "Bedre overblik giver færre overraskelser.",
            [("Gratis budget", "/budget"), ("Husk", "https://husk.itmartin.dk/om")],
            [("Ingen reklamer", "/ingen-reklamer")]),
        new(4, "Billeder og erindringer", "Lav et digitalt album til dine minder.",
            "Se på mulighederne for at samle billeder, finde dubletter og gemme historierne bag fotografierne.",
            "Minder skal være til at finde igen – ikke gemt væk i digitalt rod.",
            [("Billeder og film hjem", "/billeder-fra-skyen")],
            [("Derfor forlader jeg Facebook", "/farvel-facebook")]),
        new(5, "Fakta, nyheder og manipulation", "Lav din egen \"fakta eller fup\"-hjælper.",
            "Vi taler om sociale medier, falske billeder og videoer, manipulation, store teknologivirksomheder og de data, vi deler.",
            "Jo bedre du forstår teknologien, jo sværere bliver du at snyde.",
            [],
            [("Derfor forlader jeg Facebook", "/farvel-facebook"), ("AI og tænkning", "/ai-og-taenkning"), ("Ingen reklamer", "/ingen-reklamer"), ("Grønt og bæredygtigt", "/baeredygtighed")]),
    ];

    // Martin's own course text (pasted 2026-11-12, "just my text") - word for word, shown on the front page and /lof-aftener.
    public static readonly IReadOnlyList<string> Intro =
    [
        "AI er lidt som en trampolin. Den kan løfte dig højt – men først skal du lære at hoppe ordentligt.",
        "På dette hold tager vi det roligt og i dit eget tempo.",
    ];

    public static readonly IReadOnlyList<string> Why =
    [
        "Mange er bekymrede for kunstig intelligens: Hvad sker der med vores arbejde? Hvad betyder det for klimaet? Kan vi stadig stole på det, vi ser og læser?",
        "Vi tager bekymringerne alvorligt og ser på både mulighederne og problemerne. Men svaret er ikke nødvendigvis at lade være med at bruge AI.",
        "Lidt ligesom broccoli: Hvis man vil have børnene til at spise det, må man selv prøve det først.",
        "Prøv AI. Lær den at kende. Og brug den med omtanke.",
        "Vi ser også på, hvordan små vaner kan gøre en forskel. Når vi bruger teknologi mere bevidst – tjekker fakta, rydder op i vores digitale rod og bruger strøm på de rigtige tidspunkter – kan vi både spare penge og mindske spild.",
    ];

    public const string EveryEveningTitle = "Hver aften laver vi noget, du kan bruge derhjemme";
    public static readonly IReadOnlyList<string> EveryEvening =
    [
        "Jeg viser en af mine egne gratis digitale hjælpere, som jeg selv har lavet ved hjælp af AI.",
        "Derefter laver du din egen lille version.",
        "Du behøver ikke kunne programmere. Du beskriver med dine egne ord, hvad du gerne vil have – og AI hjælper med at bygge det.",
    ];

    public static readonly IReadOnlyList<string> NoPrior =
    [
        "Holdet er især for dig, der synes, at det hele går lidt for stærkt.",
        "Du behøver ikke kunne noget om AI på forhånd.",
        "Medbring: Din egen telefon, tablet eller bærbare computer – gerne opladt. Du må også gerne have en gratis konto hos ChatGPT eller Claude, men vi hjælper dig i gang første aften.",
        "Det vigtigste er bare, at du har lyst til at prøve.",
    ];

    public const string Teacher =
        "Martin Hvidberg har arbejdet med it og software i mange år og driver i dag ITMartin i Aarhus. Her laver han små, gratis digitale hjælpere til mennesker, der har brug for lidt ekstra – fx ordblinde, ældre og alle, der synes, teknologien går for stærkt. Martin spiller selv guitar og klaver og skriver musik, og han har brugt AI til at lave en sang til sin afdøde far. Hans holdning er enkel: AI kan gøre os bedre – men det kræver en menneskelig hjerne at bruge den rigtigt.";

    /// <summary>Show the course until its last evening is over.</summary>
    public static bool Show(Course c, DateOnly today) => today <= c.Dates[^1];

    public static string DateLine(Course c) =>
        string.Join(", ", c.Dates.Select(d => $"{d.Day}.{d.Month}")) + $" {c.Dates[^1].Year}";
}
