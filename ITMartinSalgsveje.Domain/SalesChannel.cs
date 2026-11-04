namespace ITMartinSalgsveje.Domain;

/// <summary>
/// One way to sell (a "salgsvej"): a market, a live auction, a web shop, an honesty box, a box taken out to a place ...
/// Hvordan = how it works, Hvem/Hvornår = the tasks under its own hovedopgave (MainTaskId), Resultat = what it gave.
/// </summary>
public sealed class SalesChannel : ITeamOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = SalesKinds.Marked;
    public string Status { get; set; } = SalesStatus.Idea;

    // Hvordan
    public string Place { get; set; } = string.Empty;      // "Silkeborg", "Godsbanen, Aarhus", "online"
    public string How { get; set; } = string.Empty;        // the steps
    public string Cost { get; set; } = string.Empty;       // "250 kr for en stand", "10 % af salget"
    public string Booking { get; set; } = string.Empty;    // "SMS til Ole Rask 28 34 48 78"
    public string WhatSells { get; set; } = string.Empty;  // "AA-blade og Rasmus Klump"
    public string Link { get; set; } = string.Empty;

    // Hvornår
    public DateTime? NextDate { get; set; }
    public DateTime? BookBy { get; set; }

    // Resultat
    public int? EarnedKr { get; set; }
    public bool? GoAgain { get; set; }
    public string ResultNote { get; set; } = string.Empty;

    /// <summary>The hovedopgave that holds this channel's tasks (who does what, when).</summary>
    public Guid? MainTaskId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Booking deadline passed or close (within 7 days) and still not booked.</summary>
    public bool BookSoon(DateTime today) =>
        Status != SalesStatus.Tried && BookBy is { } d && d.Date <= today.Date.AddDays(7);

    /// <summary>Next date has passed - time to write the result.</summary>
    public bool NeedsResult(DateTime today) =>
        Status == SalesStatus.Planned && NextDate is { } d && d.Date < today.Date && EarnedKr is null;
}

/// <summary>The kinds of ways to sell (user 2026-11-04: "all kinds of ways to make the sale going").</summary>
public static class SalesKinds
{
    public const string Marked = "Marked";
    public const string LiveAuktion = "Live-auktion";
    public const string Online = "Online";
    public const string Tillidsbod = "Tillidsbod";
    public const string UdMedKassen = "Ud med kassen";
    public const string Kommission = "Kommission";
    public const string LigeTilKunden = "Lige til kunden";
    public const string Kassevis = "Kassevis";
    public const string Events = "Events";

    public static readonly IReadOnlyList<string> All =
        [Marked, LiveAuktion, Online, Tillidsbod, UdMedKassen, Kommission, LigeTilKunden, Kassevis, Events];

    public static string Explain(string kind) => kind switch
    {
        Marked => "Loppemarked, kræmmermarked, bagagerumsmarked, messe - eller en lejet stand der sælger for jer.",
        LiveAuktion => "Byd live: vis tingene på video, folk byder. Godt til de sjældne ting.",
        Online => "Sælg på nettet: egen side, DBA, antikvariat.net, Discogs og lignende.",
        Tillidsbod => "En kasse med priser og MobilePay - folk betaler selv. Skal fyldes op.",
        UdMedKassen => "Tag en kasse varer med ud: plejehjem, skoler, biblioteker, arbejdspladser.",
        Kommission => "En hylde i en anden butik eller café - de tager en del af salget.",
        LigeTilKunden => "Til faste kunder: de har sagt, hvad de samler på - vi siger til, når det kommer.",
        Kassevis => "Sælg mange ting på én gang: til forhandlere, bøger på meter, rekvisitter.",
        Events => "Åbent hus, pop-up i et bibliotek eller på en festival.",
        _ => "",
    };
}

public static class SalesStatus
{
    public const string Idea = "Idé";
    public const string Planned = "Planlagt";
    public const string Tried = "Prøvet";

    public static readonly IReadOnlyList<string> All = [Idea, Planned, Tried];
}

public static class SalesChannelRules
{
    /// <summary>What is wrong, in plain Danish. Empty = can be saved.</summary>
    public static List<string> Problems(SalesChannel c)
    {
        var p = new List<string>();
        if (c.Name.Trim().Length == 0) p.Add("Skriv et navn, fx \"Loppemarked i Ry\".");
        if (c.Name.Trim().Length > 100) p.Add("Navnet er for langt (højst 100 tegn).");
        if (!SalesKinds.All.Contains(c.Kind)) p.Add("Vælg en slags salgsvej.");
        if (!SalesStatus.All.Contains(c.Status)) p.Add("Vælg om det er en idé, planlagt eller prøvet.");
        if (c.EarnedKr is < 0) p.Add("Beløbet kan ikke være under 0 kr.");
        if (c.BookBy is { } b && c.NextDate is { } n && b.Date > n.Date) p.Add("Sidste frist for at booke ligger efter datoen.");
        if (c.Link.Trim().Length > 0 && !c.Link.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
            p.Add("Linket skal starte med http:// eller https://");
        return p;
    }

    /// <summary>Næste op: planned with a date first (soonest), then planned without date, ideas, tried (best result first).</summary>
    public static IEnumerable<SalesChannel> InOrder(IEnumerable<SalesChannel> all) => all
        .OrderBy(c => c.Status == SalesStatus.Planned ? 0 : c.Status == SalesStatus.Idea ? 1 : 2)
        .ThenBy(c => c.NextDate ?? DateTime.MaxValue)
        .ThenByDescending(c => c.EarnedKr ?? 0)
        .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);
}
