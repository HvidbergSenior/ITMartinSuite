namespace ITMartinHjem.Domain;

/// <summary>One line on the front page's price list: what Martin sells, what it costs, and where to go for it.</summary>
public sealed record Offer(string Title, string Text, string Price, string Button, string Href);

/// <summary>The front page = what I sell + prices (user 2026-11-12: "I need to make my itmartin.dk super simple ...
/// What do I sell -> prices etc"). Prices are the user's own and ALL include moms: remote help 1 hour = 200 kr, photos collected/sorted =
/// 500 kr per cloud in total (1 hour remote + the sorting, user: "Get it done... And sorted... 500 ialt incl moms"), homepage: first page free, then 2/4/5 kr per day it is switched on (Selv/Sammen/Fuld hjælp; user 2026-11-12, was 49/99/149 a month). Free snebolde = only the live ones that
/// others use (ElPriser, Temperatur, Stem, Snak, Husk).</summary>
public static class PriceList
{
    // The buttons open each app's /om (Formål + Sådan bruger du den), not the app itself (user 2026-11-12: "the links go to some
    // of my personal links" - on Martin's own phone the apps open his own Husk list / Snak rooms; a visitor should read first).
    public static readonly IReadOnlyList<(string Name, string Href)> FreeSnebolde =
    [
        ("ElPriser", "https://elpriser.itmartin.dk/om"),
        ("Temperatur", "https://temperatur.itmartin.dk/om"),
        ("Stem", "https://stem.itmartin.dk/om"),
        ("Snak", "https://snak.itmartin.dk/om"),
        ("Husk", "https://husk.itmartin.dk/om"),
    ];

    public static readonly IReadOnlyList<Offer> Offers =
    [
        new("Hjælp til din pc",
            "Jeg kigger med på din skærm og hjælper dig – mens du sidder hjemme. Kun på pc.",
            "200 kr. for 1 time", "Sådan gør vi", "/hjaelp-over-skaermen"),
        new("Billeder og film fra Facebook, Instagram og Snapchat – sorteret",
            "Jeg henter alle dine filer, og vi sorterer dem – med den rigtige dato, efter år, i ét galleri. 1 time sammen over skærmen på din pc, så er det gjort.",
            "500 kr. i alt pr. tjeneste", "Sådan gør vi", "/billeder-fra-skyen"),
        new("Din egen hjemmeside – som du vil have den",
            "Intet abonnement: du betaler kun for den tid, du vil have den. Første side er gratis – og den er lavet til dig, ikke en skabelon.",
            "2 / 4 / 5 kr. om dagen", "Sådan gør vi", "/din-hjemmeside"),
        new("Bøger, tegneserier, film og musik",
            "Brugte ting fra Bogshoppen – med billede.",
            "Spørg om prisen", "Gå til Bogshoppen", "https://bogshoppen.dk"),
    ];
}
