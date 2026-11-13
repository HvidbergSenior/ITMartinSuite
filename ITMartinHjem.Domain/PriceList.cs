namespace ITMartinHjem.Domain;

/// <summary>One line on the front page's price list: what Martin sells, what it costs, and where to go for it.</summary>
/// <param name="Saving">What you save by choosing me (user 2026-11-12: "the savings to use me should be on frontpage") - shown
/// on the tile; the numbers come from the like-for-like comparison on the offer's own page.</param>
public sealed record Offer(string Title, string Text, string Price, string Button, string Href, string Saving = "");

/// <summary>The front page = what I sell + prices (user 2026-11-12: "I need to make my itmartin.dk super simple ...
/// What do I sell -> prices etc"). Prices are the user's own and ALL include moms: remote help = 300 kr per session until satisfied (later 12.11: "price for one session... until done", "i dont like hours"; was 200 kr/hour), photos collected/sorted =
/// 500 kr per cloud in total (one session until done + the sorting, user: "Get it done... And sorted... 500 ialt incl moms"), homepage (2026-11-13): first page free, then 50 % of what you pay today, help 200 kr/time also weekends (before: 10/30/40 kr per WEEK (Selv/Sammen/Fuld hjælp; user later 12.11, was 2/4/5 a day, before that 49/99/149 a month); moving an existing site = 500 kr in total, one session like the photos, user 12.11). Free snebolde = only the live ones that
/// others use (ElPriser, Temperatur, Stem, Snak, Husk).</summary>
/// <summary>A free snebold on the front page (user 2026-11-12: "all snebolde should have prices for paid and description on the
/// differences"): what the free part gives, and - when there is one - the paid part's name and what it adds. Stem has no paid part
/// (user: "It should not be paid") and is off the front page (user 2026-11-12 "remove stem").</summary>
public sealed record Snebold(string Name, string Text, string Href, string Free, string PaidName, string Paid)
{
    public bool HasPaid => PaidName.Length > 0;
}

public static class PriceList
{
    // The buttons open each app's /om (Formål + Sådan bruger du den), not the app itself (user 2026-11-12: "the links go to some
    // of my personal links" - on Martin's own phone the apps open his own Husk list / Snak rooms; a visitor should read first).
    // Temperatur left out of the front page (user 2026-11-12 "Just leave temperatur out here"). One line each = what it does.
    public static readonly IReadOnlyList<Snebold> FreeSnebolde =
    [
        new("ElPriser", "Se, hvornår strømmen er billigst i dag og i morgen.", "https://elpriser.itmartin.dk/om",
            "Prisen nu, de billigste timer og en besked, når strømmen er billig.",
            "MinElpris", "Dine egne apparater: hvad koster det at vaske, lade bilen eller tænde ovnen – og hvornår er det billigst?"),
        new("Snak", "Et privat rum til én sag – uden reklamer.", "https://snak.itmartin.dk/om",
            "Et rum til én sag med beskeder og opgaver. Det smelter, når I er færdige.",
            "Min Snak", "Alle dine rum samlet ét sted, dit eget navn på dem, og de bliver gemt længere. (Kommer snart.)"),
        new("Husk", "Din egen huskeliste – lavet af en med ADHD.", "https://husk.itmartin.dk/om",
            "I dag og i morgen – én ting ad gangen. Tilføj møder og ny post.",
            "Min Husk", "Bolde i luften: alt det, du har i gang, med ét næste skridt – og alle dine aftaler samlet under Alt."),
    ];

    /// <summary>The paid part of every snebold costs the same (user 2026-11-12: "5 kr./uge for alle") - only while it is on.</summary>
    public const string PaidPrice = "5 kr. om ugen, mens den er tændt";

    public static readonly IReadOnlyList<Offer> Offers =
    [
        new("Hjælp til din pc",
            "Jeg kigger med på din skærm og hjælper dig, mens du sidder hjemme – til du er tilfreds. Kun på pc.",
            "300 kr. pr. gang", "Sådan gør vi", "/hjaelp-over-skaermen",
            // Like for like (user 12.11): other remote PC help costs ca. 200-500 kr. per task (Handyhand price data) - no honest kr
            // saving, so the band says what is really different: a fixed price until you are satisfied.
            "Fast pris til du er tilfreds – det bliver ikke dyrere, hvis det tager længere tid"),
        new("Billeder og film fra Facebook, Instagram og Snapchat – sorteret",
            "Jeg henter alle dine filer, og vi sorterer dem – med den rigtige dato, efter år, i ét galleri. Én gang sammen over skærmen på din pc, til det er gjort.",
            "500 kr. i alt pr. tjeneste", "Sådan gør vi", "/billeder-fra-skyen",
            // No Danish market price found for sorting photos (12.11) - so no kr number, only the fixed price.
            "Fast pris: 500 kr. i alt – uanset hvor mange billeder, og hvor lang tid det tager"),
        new("Din egen hjemmeside – som du vil have den",
            // User 2026-11-13: first version with one page free, then half of what you pay today; help 200 kr/time also weekends.
            "Jeg sætter den første version op med én side – gratis. Derefter betaler du det halve af det, du betaler i dag – og kun mens den er tændt. Hjælp koster 200 kr. i timen, også i weekenden. Har du en side et andet sted, flytter jeg den for 500 kr. i alt.",
            "Første side gratis – så det halve", "Sådan gør vi", "/din-hjemmeside",
            "Du sparer altid halvdelen af det, du betaler i dag"),
        new("Bøger, tegneserier, film og musik",
            "Brugte ting fra Bogshoppen – med billede.",
            "Spørg om prisen", "Gå til Bogshoppen", "https://bogshoppen.dk"),
    ];
}
