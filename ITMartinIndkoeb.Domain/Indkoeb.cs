using System.Security.Cryptography;

namespace ITMartinIndkoeb.Domain;

// "Ugens indkøb" (2026-11-18, user: "a weekly goal... an app that can help the family with this... ONLY groceries").
// One family = one Husstand with a secret link. Everyone adds what they bought; the week answers "how much is left?".
// The plan behind it (from the family's own numbers): one big shop a week, at most 2 small top-ups, 3,000-3,500 kr a month.

public enum KoebType { Stort, Supplering }

/// <summary>The family's own limits. Defaults = the suggestion: 750 kr and 5 trips a week, a top-up at most 100 kr, a big shop from 250 kr.</summary>
public sealed record Maal(decimal UgeMaal = 750, int MaxTure = 5, decimal SupplGraense = 100, decimal StortFra = 250)
{
    public static readonly Maal Standard = new();
    public decimal MaanedFra => Math.Round(UgeMaal * 4m / 50m) * 50m;          // 750 -> 3,000
    public decimal MaanedTil => Math.Round(UgeMaal * 4.667m / 50m) * 50m;      // 750 -> 3,500

    public IReadOnlyList<string> Problemer()
    {
        var p = new List<string>();
        if (UgeMaal is < 100 or > 20000) p.Add("Ugemålet skal være mellem 100 og 20.000 kr.");
        if (MaxTure is < 1 or > 30) p.Add("Antal ture skal være mellem 1 og 30.");
        if (SupplGraense is < 10 or > 2000) p.Add("Grænsen for en supplering skal være mellem 10 og 2.000 kr.");
        if (StortFra <= SupplGraense) p.Add("Et stort indkøb skal starte over grænsen for en supplering.");
        return p;
    }
}

public sealed record Koeb(string Id, DateOnly Dato, decimal Beloeb, string Butik, string Hvem, DateTime Oprettet)
{
    public KoebType Type(Maal m) => Beloeb >= m.StortFra ? KoebType.Stort : KoebType.Supplering;
}

public sealed class Husstand
{
    public string Id { get; set; } = "";
    public string Noegle { get; set; } = "";
    public string Navn { get; set; } = "Familien";
    public Maal Maal { get; set; } = Maal.Standard;
    public List<Koeb> Koeb { get; set; } = [];
    public DateTime Oprettet { get; set; }
    /// <summary>The shared shopping list (step 3). Ticked items leave the list when the next purchase is saved (step 4).</summary>
    public List<Vare> Seddel { get; set; } = [];
    /// <summary>Where the family shops - only for "Find tilbud" (nearest stores). Postnummer, never an address.</summary>
    public Sted? Sted { get; set; }
    /// <summary>Who has taken the trip to a shop ("Lidl" -> "Eigil"). One person finds offers, another buys them.</summary>
    public Dictionary<string, string> Handler { get; set; } = [];
    /// <summary>Shops the family never goes to ("we never go to Bilka") - their offers are not shown in Find tilbud.</summary>
    public List<string> Skjulte { get; set; } = [];
    /// <summary>When the trip to a shop is planned ("Lidl" -> tuesday) - with or without someone who has taken it.</summary>
    public Dictionary<string, DateOnly> TurDag { get; set; } = [];
    /// <summary>The shop apps / member cards the family has ("Lidl Plus", "Netto+", "Coop-appen") - decides the price shown.</summary>
    public List<string> Apps { get; set; } = [];

    public bool HarApp(string? app) => app is not null && Apps.Contains(AppPriser.Navn(app), StringComparer.CurrentCultureIgnoreCase);

    public bool Skjult(string butik) => Skjulte.Contains(SeddelRegler.Gruppe(butik), StringComparer.CurrentCultureIgnoreCase);

    public static Husstand Ny(string? navn, DateTime nowUtc) => new()
    {
        Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant(),
        Noegle = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_'),
        Navn = RenTekst(navn, 40) is { Length: > 0 } n ? n : "Familien",
        Oprettet = nowUtc,
    };

    public static string RenTekst(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length > max ? t[..max] : t;
    }
}

/// <summary>One line on the shopping list. Butik = where to buy it (from the offer, or chosen), null = anywhere.
/// Tilbud = the offer it came from ("30 kr, til fredag"), if any.</summary>
public sealed record Vare(string Id, string Tekst, string Hvem, string? Butik, string? Tilbud, bool Koebt, DateTime Oprettet);

public sealed record Sted(string Postnr, string Navn, double Lat, double Lng);

public static class SeddelRegler
{
    public const int MaxVarer = 150;
    public const int MaxTekst = 80;

    /// <summary>Error text, or null.</summary>
    public static string? Problem(string? tekst, int antal)
    {
        if (string.IsNullOrWhiteSpace(tekst)) return "Skriv hvad der skal købes, fx mælk.";
        if (antal >= MaxVarer) return $"Sedlen er fuld ({MaxVarer} varer). Fjern de købte først.";
        return null;
    }

    public static bool GyldigtPostnr(string? p) => p is { Length: 4 } && p.All(char.IsAsciiDigit) && p[0] != '0';

    public const string HvorSomHelst = "Hvor som helst";

    /// <summary>The list's group for an item: its shop (offer chains like "REMA 1000" -> "Rema 1000"), or "Hvor som helst".</summary>
    public static string Gruppe(string? butik) => string.IsNullOrWhiteSpace(butik) ? HvorSomHelst
        : Regler.ButikFra(butik) is var b && b != "Andet" ? b : butik.Trim();

    /// <summary>A purchase in <paramref name="butik"/> is saved: what was ticked there (and ticked "anywhere" items) is bought
    /// and leaves the list, and the trip is no longer taken.</summary>
    public static int Handlet(Husstand h, string butik)
    {
        var g = Gruppe(butik);
        h.Handler.Remove(g);
        h.TurDag.Remove(g);
        return h.Seddel.RemoveAll(v => v.Koebt && (Gruppe(v.Butik) == g || Gruppe(v.Butik) == HvorSomHelst || g == "Andet"));
    }
}

/// <summary>The price the family really pays. ManglerApp = the offer is only for app users and the family does not have it.</summary>
public sealed record VoresPris(decimal Pris, string? Note, bool ManglerApp, string? App);

/// <summary>User 2026-11-19: "what about offers that are applied if you have card? Like Lidl Plus?"
/// Tilbud reads three kinds from the leaflets: only with the app (Lidl Plus), a lower price with the app on top of the
/// normal one (Netto+ "+ PRIS", føtex/Bilka "plus pris", Coop "medlemspris"), and the price without the app if printed.</summary>
public static class AppPriser
{
    /// <summary>The leaflets' many spellings -> one name per app ("Lidl Plus-appen" -> "Lidl Plus").</summary>
    public static string Navn(string app)
    {
        var a = app.ToLowerInvariant();
        if (a.Contains("lidl")) return "Lidl Plus";
        if (a.Contains("netto")) return "Netto+";
        if (a.Contains("føtex") || a.Contains("fotex")) return "føtex Plus";
        if (a.Contains("bilka")) return "Bilka Plus";
        if (a.Contains("coop") || a.Contains("medlem")) return "Coop-appen";
        if (a.Contains("rema")) return "REMA-appen";
        var t = app.Trim();
        foreach (var end in new[] { "-appen", " appen", "appen" })
            if (t.EndsWith(end, StringComparison.OrdinalIgnoreCase)) return t[..^end.Length].Trim();
        return t;
    }

    public static readonly string[] Kendte = ["Lidl Plus", "Netto+", "føtex Plus", "Bilka Plus", "Coop-appen", "REMA-appen"];

    public static VoresPris For(decimal pris, string? kunMedApp, string? appNavn, decimal? appPris, decimal? udenAppPris, Func<string, bool> harApp)
    {
        static string Kr(decimal v) => v.ToString(v % 1 == 0 ? "0" : "0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
        if (kunMedApp is not null)
        {
            var app = Navn(kunMedApp);
            if (harApp(app)) return new(pris, $"med {app}", false, app);
            return udenAppPris is { } u
                ? new(u, $"{Kr(pris)} kr med {app}", false, app)
                : new(pris, $"kun med {app}", true, app);
        }
        if (appNavn is not null)
        {
            var app = Navn(appNavn);
            if (appPris is { } ap && ap < pris)
                return harApp(app) ? new(ap, $"med {app} (ellers {Kr(pris)} kr)", false, app) : new(pris, $"{Kr(ap)} kr med {app}", false, app);
            return new(pris, harApp(app) ? $"ekstra tilbud i {app}" : null, false, app);
        }
        return new(pris, null, false, null);
    }
}

public enum UgeTilstand { Godt, PasPaa, Over }

/// <summary>The week as the family sees it: spent, left, trips, and ONE next step (process, not numbers only).</summary>
public sealed record UgeStatus(DateOnly Mandag, decimal Brugt, decimal Tilbage, int Ture, int StoreIndkoeb, int Suppleringer,
    UgeTilstand Tilstand, string NaesteSkridt, IReadOnlyList<string> Advarsler);

public sealed record MaanedStatus(int Aar, int Maaned, decimal Brugt, int Ture, decimal Fra, decimal Til, bool Faerdig);

public static class Regler
{
    public static readonly string[] Butikker = ["Lidl", "Føtex", "Netto", "Rema 1000", "Løvbjerg", "Andet"];

    /// <summary>"LIDL DANMARK K/S" -> "Lidl", "føtex Skejby" -> "Føtex"; anything else -> "Andet".</summary>
    public static string ButikFra(string? forretning)
    {
        var f = (forretning ?? "").ToLowerInvariant();
        if (f.Contains("lidl")) return "Lidl";
        if (f.Contains("føtex") || f.Contains("fotex") || f.Contains("foetex")) return "Føtex";
        if (f.Contains("netto")) return "Netto";
        if (f.Contains("rema")) return "Rema 1000";
        if (f.Contains("løvbjerg") || f.Contains("lovbjerg") || f.Contains("loevbjerg")) return "Løvbjerg";
        return "Andet";
    }

    public static DateOnly Mandag(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>Error text, or null.</summary>
    public static string? KoebProblem(decimal beloeb, string? butik, DateOnly dato, DateOnly idag)
    {
        if (beloeb <= 0) return "Skriv beløbet, fx 245.";
        if (beloeb > 10000) return "Det er vist for meget til ét indkøb. Tjek beløbet.";
        if (string.IsNullOrWhiteSpace(butik)) return "Vælg butikken.";
        if (dato > idag) return "Datoen kan ikke være i fremtiden.";
        if (dato < idag.AddDays(-62)) return "Datoen er for gammel.";
        return null;
    }

    public static UgeStatus Uge(IEnumerable<Koeb> alle, Maal m, DateOnly idag)
    {
        var mandag = Mandag(idag);
        var uge = alle.Where(k => k.Dato >= mandag && k.Dato <= idag).ToList();
        var brugt = uge.Sum(k => k.Beloeb);
        var store = uge.Count(k => k.Type(m) == KoebType.Stort);
        var suppl = uge.Count - store;
        var tilbage = m.UgeMaal - brugt;
        var advarsler = new List<string>();
        foreach (var k in uge.Where(k => k.Type(m) == KoebType.Supplering && k.Beloeb > m.SupplGraense))
            advarsler.Add($"Supplering i {k.Butik} på {Kr(k.Beloeb)} kr – over grænsen på {Kr(m.SupplGraense)} kr.");
        if (uge.Count > m.MaxTure) advarsler.Add($"{uge.Count} ture – målet er højst {m.MaxTure}.");

        var tilstand = tilbage < 0 ? UgeTilstand.Over
            : tilbage < m.UgeMaal * 0.2m || uge.Count >= m.MaxTure ? UgeTilstand.PasPaa
            : UgeTilstand.Godt;
        var dageTilbage = 7 - (idag.DayNumber - mandag.DayNumber);   // incl. today

        string naeste;
        if (tilbage < 0) naeste = $"Ugen er brugt. Spis af det, I har, til mandag – så starter en ny uge med {Kr(m.UgeMaal)} kr.";
        else if (store == 0) naeste = $"Lav ugens store indkøb med indkøbsseddel – planlæg ca. {Kr(Math.Min(tilbage, m.UgeMaal * 0.6m))} kr.";
        else if (uge.Count >= m.MaxTure) naeste = "Alle ugens ture er brugt. Kan resten vente til mandag?";
        else naeste = $"{Kr(tilbage)} kr tilbage til {dageTilbage} {(dageTilbage == 1 ? "dag" : "dage")}. Supplering: højst {Kr(Math.Min(m.SupplGraense, tilbage))} kr ad gangen.";

        return new UgeStatus(mandag, brugt, tilbage, uge.Count, store, suppl, tilstand, naeste, advarsler);
    }

    public static MaanedStatus Maaned(IEnumerable<Koeb> alle, Maal m, int aar, int maaned, DateOnly idag)
    {
        var k = alle.Where(x => x.Dato.Year == aar && x.Dato.Month == maaned).ToList();
        var faerdig = new DateOnly(aar, maaned, 1).AddMonths(1) <= idag;
        return new MaanedStatus(aar, maaned, k.Sum(x => x.Beloeb), k.Count, m.MaanedFra, m.MaanedTil, faerdig);
    }

    public static string Kr(decimal v) => v.ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
}
