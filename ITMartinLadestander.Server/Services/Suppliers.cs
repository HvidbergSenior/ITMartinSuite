namespace ITMartinLadestander.Server.Services;

public sealed record Supplier(
    string Name, string Model, string UpFront, string Running, string ResidentPrice, string Verdict,
    string Tag, string TagKind, string InvestKr, string PaidOver5YrsKr, string IncomeToForening, string Url);

// Listepriser from the suppliers' own pages, September 2026. Rough 5-year
// totals are for 4 outlets and 30 MWh/yr (≈10 EVs).
public static class Suppliers
{
    public static readonly IReadOnlyList<Supplier> All =
    [
        new("Clever Boligforening", "Operatøren ejer alt",
            "19.000 kr ekskl. moms pr. udtag, 5-års leje – 4 udtag ≈ 76.000 ekskl. / 95.000 inkl. moms. 20 m gravning inkl.",
            "0 kr/md – Clever drifter alt",
            "Clever Go ca. 3,49 kr/kWh eller Clever One 999 kr/md + energitillæg",
            "Nul administration, men dyrest for brugerne og ingen indtægt til foreningen.",
            "Topkarakter i forespørgslen", "ok", "≈ 95.000", "0", "0 (brugerne betaler Clever)",
            "https://clever.dk/boligforeninger/boligforening/"),
        new("OK Total", "Operatøren ejer alt",
            "Fra 40.000 kr ekskl. moms for 4 udtag (60.000/6, 80.000/8, 100.000/10). 30 m gravning inkl. 5 års binding.",
            "0 kr/md",
            "OK's månedspris: 2,55 kr/kWh (sep. 2026), 2,39 (aug.), 2,15 (jul.)",
            "Billigste kWh-pris og godt bedømt – men OK kræver min. 40 enheder, vi har 30. Spørg alligevel.",
            "Kræver min. 40 enheder", "w", "≈ 50.000", "0", "0 (brugerne betaler OK)",
            "https://www.ok.dk/privat/produkter/opladning/ladestandere-til-din-boligforening/ok-total"),
        new("Spirii (platform)", "Foreningen ejer, Spirii afregner",
            "Foreningen køber selv standerne",
            "Serviceaftale ca. 59–79 kr/md",
            "Foreningen sætter prisen. Rå elpris ca. 1,60–1,67 kr/kWh med FDM-aftale (1,12 kr/kWh afgiftsrefusion)",
            "Kernen i Spiriis forretning er delte standere med afregning pr. bruger.",
            "5,0 i forespørgslen", "ok", "≈ 60–80.000", "≈ 4.500", "Margen sættes af foreningen",
            "https://www.spirii.com/da/pressemeddelelser/fdm-og-spirii-gar-sammen-om-at-tilbyde-markedets-mest-attraktive-ladelosning"),
        new("Egne standere + platform", "Foreningen ejer (Zaptec Pro + Monta / Base2Charge / NRGi Zapp)",
            "Zaptec Pro ca. 14.500 kr inkl. moms pr. stander (2 udtag) + gravning. Ingen binding.",
            "Lille platformgebyr pr. udtag eller pr. kWh",
            "Foreningen sætter fx 2,75 kr/kWh og beholder margen + afgiftsrefusion. Kø/reservation i app (Zapp).",
            "Billigst over 5 år, tilbagebetalt på under 2 år ifølge Zaptec. Mest arbejde for bestyrelsen. NRGi er allerede vores elleverandør.",
            "Åben API", "ok", "≈ 60–80.000", "≈ 5–15.000", "≈ 0,3–0,5 kr/kWh ≈ 10–15.000 kr/år",
            "https://zapp.dk/pages/housing-associations"),
        new("Looad (blive)", "Foreningen ejer måler + elaftale, Looad afregner",
            "0 kr",
            "79 kr/md + 40 øre/kWh (≈ 12.000 kr/år ved 30 MWh)",
            "ca. 2,3–2,8 kr/kWh (fremgår ikke af opgørelsen)",
            "Ingen investering, men prisrisiko hos foreningen og et cut, der vokser med forbruget.",
            "Nuværende", "w", "0", "≈ 66.000", "≈ 0,24 kr/kWh ved 2024-priser – negativ hvis spot stiger 0,30",
            "/looad"),
    ];

    public static readonly IReadOnlyList<(string Text, string Url)> Sources =
    [
        ("Clever boligforening", "https://clever.dk/boligforeninger/boligforening/"),
        ("Clever One 2026", "https://ladetjek.dk/guides/clever-one-pris-2026"),
        ("OK Total", "https://www.ok.dk/privat/produkter/opladning/ladestandere-til-din-boligforening/ok-total"),
        ("OK listepriser", "https://www.ok.dk/erhverv/produkter/ladestandere/listepriser"),
        ("FDM + Spirii", "https://www.spirii.com/da/pressemeddelelser/fdm-og-spirii-gar-sammen-om-at-tilbyde-markedets-mest-attraktive-ladelosning"),
        ("Zaptec", "https://www.zaptec.com/da/ladeloesninger/zaptec-pro/ladestander-til-boligforeninger"),
        ("Egedal El", "https://egedalel.dk/godt-nyt/zaptec-elbil-opladning-forening-erhverv/"),
        ("NRGi Zapp", "https://zapp.dk/pages/housing-associations"),
        ("Base2Charge", "https://base2charge.com/boligforening/"),
    ];
}
