namespace ITMartinHjem.Server.Services;

// The "Vidste du …" facts (Components/VidsteDu) per page: concrete things first - the box starts on one of those.
// Facts are the user's own words/numbers or sourced (source in the comment). Href = where the button goes.
public static class SiteFacts
{
    public sealed record Fact(string Title, string Href, string Text);

    // itmartin.dk front page: the small, concrete help Martin gives (user 2026-10-27: "Facebook bæredygtighed etc should not be
    // dominant... The concrete examples should"). Removed 2026-10-27 (user): "mail, kalender og billeder ... de samme på telefon og PC".
    public static readonly IReadOnlyList<Fact> ItMartin =
    [
        // User 2026-10-27: "small everyday trouble on phone or pc, gives huge satisfaction when fixed. I do that".
        new("Skriv til mig", "/chat-med-martin", "… at små, irriterende ting på telefonen eller PC'en – printeren, der driller, mailen, der ikke kommer, opdateringen, der hænger – giver en kæmpe lettelse, når de er løst? Det hjælper jeg med."),
        new("Skriv til mig", "/chat-med-martin", "… at »lageret er fuldt« på telefonen tit kan løses på et kvarter – uden at du mister et eneste billede?"),
        new("Hjælp på din skærm", "/mine-apps?app=Se%20med", "… at jeg kan flytte dine billeder, kontakter og apps fra den gamle telefon til den nye sammen med dig – så intet bliver væk?"),
        new("Skriv til mig", "/chat-med-martin", "… at en langsom PC tit kan blive hurtig igen uden at købe en ny? Ofte er det bare nogle programmer, der starter af sig selv."),
        // User 2026-10-27: "make more that reflects what I can help with" - from what Martin actually does (billeder, Se med,
        // Budget, ElPriser, hjemmesider, særlige behov).
        new("Tjek dine billeder", "/mine-apps?app=Tjek%20dine%20billeder", "… at billederne på en gammel telefon i skuffen eller en gammel computer sagtens kan reddes – og samles ét sted, sorteret efter år?"),
        new("Tjek dine billeder", "/mine-apps?app=Tjek%20dine%20billeder", "… at når iCloud eller Google Fotos siger »fuld«, kan jeg få dine billeder hjem på din egen disk – så du ikke skal betale hver måned?"),
        new("Skriv til mig", "/chat-med-martin", "… at jeg kan sætte din nye PC op sammen med dig – flytte filer, mail og printer – så den virker som den gamle, bare hurtigere?"),
        new("Skriv til mig", "/chat-med-martin", "… at den mail, det dokument eller det billede, du er sikker på er væk, tit kan findes igen?"),
        new("Budget", "/budget", "… at mange betaler for abonnementer, de har glemt? Jeg kan hjælpe dig med at finde dem i dit kontoudtog."),
        new("ElPriser", "/mine-apps?app=ElPriser", "… at du kan spare på strømmen ved at starte opvask og vask, når prisen er lavest – og at ElPriser viser dig hvornår?"),
        new("Har du en idé?", "/start-din-virksomhed", "… at jeg kan lave en enkel hjemmeside til din forening eller dit lille firma – som du selv kan skrive i fra telefonen?"),
        new("Særlige behov", "/ydelser/adhd-og-autisme", "… at jeg kan gøre telefonen eller PC'en enklere for en, der har svært ved den – kun de apps, der bliver brugt, og store, tydelige knapper?"),
        // User 2026-10-27: "at jeg hjælper mennesker med særlige udfordringer. Kan give råd til bekymrede forældre og/eller mennesker
        // som er i tvivl om hvad de skal gøre" (ADHD, pædagog 5 år på botilbud - see page ydelser/adhd-og-autisme).
        new("Særlige behov", "/ydelser/adhd-og-autisme", "… at jeg hjælper mennesker med særlige udfordringer – og gerne giver råd til bekymrede forældre eller dig, der er i tvivl om, hvad du skal gøre?"),
        // User 2026-10-27: "and what I can do" - things Martin has built and can make for you.
        new("Polstrer", "/mine-apps?app=Polstrer", "… at jeg kan lave en app til netop dit arbejde – som til møbelpolstreren, der tager billeder af hvert trin og husker sine teknikker?"),
        // User 2026-10-27: "at jeg laver ALT selv. Hjemmeside, apps, musik".
        new("Om mig", "/om-itmartin", "… at jeg laver ALT selv – hjemmesiden, alle apps og musikken? Så kan jeg også lave det, du har brug for."),
        new("Tjek dine billeder", "/billeder", "… at jeg kan lave et familiegalleri med kode, så kun familien ser billederne – på telefonen, uden Facebook?"),
        new("Skriv til mig", "/chat-med-martin", "… at jeg kan lave en fødselsdagsside med billeder og hilsner, som hele familien kan se og skrive på?"),
        new("Notatskriver", "/mine-apps?app=Notatskriver", "… at Notatskriver kan skrive et referat af et møde – og at lyden aldrig forlader din egen computer?"),
        new("Tilbud", "/mine-apps?app=Tilbud", "… at Tilbud finder dine varer på tilbud i butikkerne nær dig – uden butikkernes egne apps?"),
        new("Om mig", "/om-itmartin", "… at jeg har 20 års erfaring som systemudvikler – og kan hjælpe din virksomhed med et system, der driller?"),
        // User 2026-10-27: "At jeg af princip IKKE - kig på artifacts about my principles" -> artifact "Små, rene snebolde" ("nogle ting,
        // jeg aldrig vil gøre til en del af snebolden") + memory: no ads ever, no big clouds.
        new("Priser", "/apps", "… at jeg af princip IKKE sælger timer? Du betaler ikke mere, fordi noget tog længere tid at få til at virke."),
        new("Det de store ikke fortæller", "/dine-data", "… at jeg af princip IKKE samler data, bare fordi jeg kan? Kan en app virke uden dine oplysninger, så skal den det."),
        new("Ingen reklamer", "/ingen-reklamer", "… at jeg af princip IKKE har reklamer i mine hjælpere – og aldrig får det?"),
        new("Om mig", "/om-itmartin", "… at jeg af princip IKKE tilføjer en funktion, bare fordi jeg kan? Bliver noget for kompliceret, spørger jeg først: Hvad kan vi fjerne?"),
        new("Om mig", "/om-itmartin", "… at jeg af princip IKKE bruger Google, Amazon eller Microsofts store skyer? Mine hjælpere kører på mine egne servere i Danmark."),
        // User 2026-10-27: "...hader subscriptions. At noget er billigt i starten, men bliver dyrere og dyrere som frøen i kogende vand".
        new("Priser", "/apps", "… at jeg hader abonnementer, der er billige i starten og så bliver dyrere og dyrere? Det er som frøen i vandet, der langsomt bliver varmet op – man opdager det først, når det er for sent."),
        // User 2026-10-27: "... hælder mod openSource".
        new("Om mig", "/om-itmartin", "… at jeg hælder mod open source? Så bliver du ikke låst fast til ét firmas programmer – og andre kan se, hvordan det virker."),
        new("Om mig", "/om-itmartin", "… at jeg af princip IKKE bygger for at blive stor? En app behøver ikke tusindvis af brugere – den skal løse et rigtigt problem for nogen."),
        // User 2026-10-27: "jeg kan finde priser på dine ting... Og modtager gerne ting som I vil af med".
        new("Skriv til mig", "/chat-med-martin", "… at jeg kan finde ud af, hvad dine ting er værd – bøger, tegneserier, film, musik og spil?"),
        new("Bogshoppen", "https://bogshoppen.dk", "… at jeg gerne tager imod de ting, du vil af med? Så får de nye ejere i stedet for at ende i containeren."),
    ];

    // bogshoppen.dk (user 2026-10-27: "vidste du should be on Bogshoppen.dk as well"): Bogshoppen first, then a few of
    // Martin's - soon one company (Components/OneCompany). Numbers from the bogshoppen page text (100.000+, ~40 years, 30+ years friends).
    public static readonly IReadOnlyList<Fact> Bogshoppen =
    [
        new("Skriv mig op – gratis", "#foelg", "… at Bogshoppen har over 100.000 bøger, blade og tegneserier, der skal finde nye ejere?"),
        new("Om Bogshoppen", "/om-bogshoppen", "… at Bogshoppen har handlet med bøger, tegneserier, film, musik og brætspil i omkring 40 år?"),
        new("Om Bogshoppen", "/om-bogshoppen", "… at Rico og Martin har været venner i over 30 år?"),
        new("Skriv til os", "https://itmartin.dk/chat-med-martin", "… at vi gerne tager imod de ting, du vil af med? Så får de nye ejere i stedet for at ende i containeren."),
        new("Skriv til os", "https://itmartin.dk/chat-med-martin", "… at vi kan finde ud af, hvad dine ting er værd – bøger, tegneserier, film, musik og spil?"),
        new("Skriv mig op – gratis", "#foelg", "… at det er gratis at skrive sig op – og at du får besked og et login, når den første liveauktion starter?"),
        new("Se de gratis apps", "https://itmartin.dk/mine-apps", "… at dit kundenummer også giver dig Martins gratis apps på itmartin.dk?"),
    ];

    /// <summary>Bogshoppen's own facts, then the first itmartin.dk ones with full links (they live on itmartin.dk).</summary>
    public static IReadOnlyList<Fact> ForBogshoppen() =>
        [.. Bogshoppen, .. ItMartin.Take(6).Select(f => f with { Href = f.Href.StartsWith('/') ? "https://itmartin.dk" + f.Href : f.Href })];

    // Mine apps (user 2026-10-27: the "Om mine apps" boxes "should have its own") - from the user's own texts there (2026-10-14).
    public static readonly IReadOnlyList<Fact> MineApps =
    [
        new("Om mine hjælpere", "#om-mine-apps", "… at mine apps ikke skal downloades? Du åbner dem bare i browseren – på mobilen, tabletten eller computeren."),
        new("Skriv mig op – gratis", "/#bliv-kunde", "… at du skriver dig op én gang – og så får et kundenummer til alle de gratis apps?"),
        new("Om mine hjælpere", "#om-mine-apps", "… at det er webappen, man laver først? Mobilappen laver man senere – og den mister faktisk nogle af funktionerne på vejen."),
        new("Om mine hjælpere", "#om-mine-apps", "… at vi typisk sad 10–15 mand på webappen i de store it-systemer – og én fyr lavede mobildelen?"),
        new("Om mine hjælpere", "#om-mine-apps", "… at mobilappen tit er den måde, man lettest holder fast i kunderne – så de bliver ved med at betale og give deres oplysninger fra sig?"),
        new("Skriv til mig", "/chat-med-martin", "… at mange har apps på telefonen, de ikke tør slette, fordi de ikke ved, hvad de gør? Det kan jeg hjælpe dig med."),
        new("Send mig navnet", "/chat-med-martin", "… at du kan sende mig navnet på en mobilapp – så laver jeg en udgave af den, som du vil have den?"),
    ];
}
