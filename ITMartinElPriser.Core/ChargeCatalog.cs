namespace ITMartinElPriser.Core;

/// <summary>Where the car gets its power. The comparison prices each place separately.</summary>
public enum ChargeSpot { Hjem, Forening, Arbejde, Offentlig, Lyn }

/// <summary>
/// One way to pay for charging. Prices are kr/kWh incl. moms for the places it covers; places it does not cover
/// fall back to the cheapest pay-as-you-go price. Every product carries its source and the date it was checked,
/// because charging prices change often and have no public data feed (unlike electricity contracts on elpris.dk).
/// </summary>
public sealed record ChargeProduct(
    string Id,
    string Company,
    string Name,
    string Kind,                                   // "Fri opladning", "Abonnement", "Pr. forbrug", "Ladeboks"
    double MonthlyFee,
    IReadOnlyDictionary<ChargeSpot, double> Prices,
    string Note,
    string Source,
    DateOnly Checked)
{
    /// <summary>kWh per month included in the fee (the first ones cost nothing), then the normal prices apply.</summary>
    public double IncludedKwh { get; init; }
    /// <summary>Extra kr/kWh on top of the covered prices when power is expensive (Clever's "energitillæg").</summary>
    public double SurchargePerKwh { get; init; }
    /// <summary>One-off price (e.g. installation of the box), shown separately - not in the monthly cost.</summary>
    public double OneOff { get; init; }
    /// <summary>Home price is the household's own electricity price (spot-based) - filled in at comparison time.</summary>
    public bool HomeAtOwnPrice { get; init; }
    /// <summary>The product brings a home charging box (rented or bought).</summary>
    public bool ProvidesHomeBox { get; init; }
    /// <summary>Only for plug-in hybrids / only for full EVs, if it differs.</summary>
    public double? HybridMonthlyFee { get; init; }
    /// <summary>Covers the association charger only when that charger is run by this company.</summary>
    public string? CoversForeningIfOperator { get; init; }
    /// <summary>The figure needs re-checking before anyone relies on it.</summary>
    public bool Unverified { get; init; }
}

/// <summary>Pay-as-you-go prices on the public network - the fallback for every place a product does not cover.</summary>
public sealed record PublicPrice(string Network, ChargeSpot Spot, double KrPerKwh, string Source, DateOnly Checked, bool Unverified = false);

/// <summary>Typical consumption from the socket (incl. ~10 % charging loss), kWh per 100 km, mixed Danish driving.</summary>
public sealed record CarModel(string Name, double KwhPer100Km);

public static class ChargeCatalog
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    private static Dictionary<ChargeSpot, double> P(params (ChargeSpot s, double kr)[] p) => p.ToDictionary(x => x.s, x => x.kr);

    public static readonly IReadOnlyList<PublicPrice> PayAsYouGo =
    [
        new("Norlys", ChargeSpot.Offentlig, 2.99, "https://mobilsiden.dk/?p=256568", new(2026, 1, 29)),
        new("Clever Go", ChargeSpot.Offentlig, 3.49, "https://clever.dk/ladeloesninger/", Oct1),
        new("Norlys", ChargeSpot.Lyn, 3.29, "https://mobilsiden.dk/?p=256568", new(2026, 1, 29)),
        new("Circle K", ChargeSpot.Lyn, 3.59, "https://mobilsiden.dk/?p=210843", new(2026, 1, 1), Unverified: true),
        new("Q8", ChargeSpot.Lyn, 3.59, "https://mobilsiden.dk/?p=210843", new(2026, 1, 1), Unverified: true),
        new("Tesla (andre mærker)", ChargeSpot.Lyn, 3.60, "https://mobilsiden.dk/?p=265272", new(2026, 5, 29)),
        new("Ionity", ChargeSpot.Lyn, 3.90, "https://mobilsiden.dk/?p=210843", new(2026, 1, 1), Unverified: true),
        new("Clever Go", ChargeSpot.Lyn, 3.99, "https://clever.dk/ladeloesninger/", Oct1),
    ];

    public static readonly IReadOnlyList<ChargeProduct> Products =
    [
        new("clever-one", "Clever", "Clever One uden ladeboks", "Fri opladning", 999,
            P((ChargeSpot.Offentlig, 0), (ChargeSpot.Lyn, 0)),
            "Fri opladning på Clevers net (60.000+ ladepunkter). Energitillæg, når strømmen er dyr (0,29 kr./kWh i august 2026).",
            "https://clever.dk/ladeloesninger/", Oct1)
        { SurchargePerKwh = 0.29, HybridMonthlyFee = 499, CoversForeningIfOperator = "clever" },

        new("clever-one-box", "Clever", "Clever One med ladeboks", "Fri opladning", 849,
            P((ChargeSpot.Hjem, 0), (ChargeSpot.Offentlig, 0), (ChargeSpot.Lyn, 0)),
            "Fri opladning hjemme og på Clevers net, ladeboks inkl. Energitillæg når strømmen er dyr.",
            "https://clever.dk/ladeloesninger/", Oct1)
        { SurchargePerKwh = 0.29, OneOff = 2999, ProvidesHomeBox = true, CoversForeningIfOperator = "clever" },

        new("clever-you", "Clever", "Clever You", "Ladeboks", 240,
            P((ChargeSpot.Hjem, 0)),
            "Leje af ladeboks; hjemme betaler du den aktuelle strømpris. Ude: Clever Go-priser. 6 mdr. binding (mindstepris 4.439 kr.).",
            "https://clever.dk/ladeloesninger/", Oct1)
        { HomeAtOwnPrice = true, OneOff = 2999, ProvidesHomeBox = true },

        new("norlys-ude-15", "Norlys", "Norlys Oplad Ude 15", "Abonnement", 499,
            P((ChargeSpot.Offentlig, 2.99), (ChargeSpot.Lyn, 3.29)),
            "250 kWh om måneden på Norlys' offentlige ladere; derover normal pris. Energitillæg, når strømmen i snit er over 0,89 kr./kWh.",
            "https://mobilsiden.dk/?p=256568", new(2026, 1, 29))
        { IncludedKwh = 250 },

        new("norlys-ude-25", "Norlys", "Norlys Oplad Ude 25", "Abonnement", 699,
            P((ChargeSpot.Offentlig, 2.99), (ChargeSpot.Lyn, 3.29)),
            "400 kWh om måneden på Norlys' offentlige ladere; derover normal pris. Energitillæg, når strømmen i snit er over 0,89 kr./kWh.",
            "https://mobilsiden.dk/?p=256568", new(2026, 1, 29))
        { IncludedKwh = 400 },

        new("ok-hjem", "OK", "OK ladeboks (leje og service)", "Ladeboks", 169,
            P((ChargeSpot.Hjem, 0), (ChargeSpot.Offentlig, 3.00), (ChargeSpot.Lyn, 3.00)),
            "Leje af ladeboks; hjemme betaler du din strømpris. OK har også abonnementer til 45-99 kr./md. med lavere kWh-pris ude – priserne skal tjekkes.",
            "https://mobilsiden.dk/?p=250344", new(2025, 12, 8))
        { HomeAtOwnPrice = true, ProvidesHomeBox = true, Unverified = true },

        new("tesla-sub", "Tesla", "Tesla Supercharger-abonnement", "Abonnement", 85,
            P((ChargeSpot.Lyn, 2.60)),
            "Fast pris 2,60 kr./kWh på Teslas ca. 40 danske Supercharger-stationer – for alle mærker.",
            "https://mobilsiden.dk/?p=265272", new(2026, 5, 29)),

        new("ionity-motion", "Ionity", "Ionity Passport Motion", "Abonnement", 45,
            P((ChargeSpot.Lyn, 2.80)), "Lavere lynpris på Ionitys stationer.",
            "https://mobilsiden.dk/?p=210843", new(2026, 1, 1)) { Unverified = true },

        new("ionity-power", "Ionity", "Ionity Passport Power", "Abonnement", 99,
            P((ChargeSpot.Lyn, 2.20)), "Lavere lynpris på Ionitys stationer.",
            "https://mobilsiden.dk/?p=210843", new(2026, 1, 1)) { Unverified = true },
    ];

    /// <summary>The association charger's price per operator - the user can override it with their own price.</summary>
    public static readonly IReadOnlyList<(string Id, string Name, double KrPerKwh, string Note)> AssociationOperators =
    [
        ("egen", "Egne ladere (fx Spirii)", 2.38, "Skelagerhøjens skøn: kostpris ≈ 2,00 + 30 øre tillæg ekskl. moms"),
        ("clever", "Clever (Clever Link)", 2.99, "Clever Link, oktober 2026 – reguleres hver måned"),
        ("ok", "OK Total", 2.55, "OK's foreningspris, september 2026 – skifter hver måned"),
        ("looad", "Looad", 2.78, "Skelagerhøjen, snit feb.–aug. 2026"),
    ];

    /// <summary>Typical consumption from the socket, kWh per 100 km incl. charging loss. Users can type their own.</summary>
    public static readonly IReadOnlyList<CarModel> Cars =
    [
        new("Tesla Model 3", 16.5), new("Tesla Model Y", 18.5), new("Volkswagen ID.3", 18), new("Volkswagen ID.4", 20.5),
        new("Skoda Enyaq", 20), new("Skoda Elroq", 19), new("Kia EV3", 17.5), new("Kia Niro EV", 18), new("Kia EV6", 20),
        new("Hyundai Kona Electric", 17), new("Hyundai Ioniq 5", 20.5), new("Polestar 2", 20.5), new("Volvo EX30", 18.5),
        new("Volvo EX40 / XC40", 22.5), new("MG4", 18.5), new("BYD Atto 3", 19), new("BYD Dolphin", 17), new("Renault Megane E-Tech", 17.5),
        new("Renault 5", 16), new("Peugeot e-208", 16.5), new("Peugeot e-2008", 18), new("Nissan Leaf", 18.5), new("Audi Q4 e-tron", 21),
        new("BMW i4", 19), new("BMW iX1", 19.5), new("Cupra Born", 18), new("Toyota bZ4X", 20), new("Ford Mustang Mach-E", 21.5),
        new("Mercedes EQA", 19.5), new("Fiat 500e", 15.5),
    ];
}
