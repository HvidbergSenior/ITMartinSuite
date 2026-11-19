using ITMartinIndkoeb.Domain;

namespace ITMartinIndkoeb.Application;

public interface IHusstandStore
{
    Husstand? Hent(string noegle);
    void Gem(Husstand h);
}

/// <summary>The Tilbud app (GET /api/tilbud + /api/sted). Indkoeb__TilbudUrl; the apps stay separate.</summary>
public interface ITilbudKilde
{
    Task<IReadOnlyList<TilbudVisning>> SoegAsync(string q, double lat, double lng, CancellationToken ct = default);
    Task<Sted?> StedAsync(string postnr, CancellationToken ct = default);
}

public sealed record TilbudVisning(string Butik, string Titel, string? Beskrivelse, decimal Pris, decimal? Foer, string? Enhedspris,
    DateOnly? Til, string? Billede, string? KunMedApp, string? NaermesteButik, double? Km, bool AndenVare = false,
    string? AppNavn = null, decimal? AppPris = null, decimal? UdenAppPris = null,
    decimal? VoresPris = null, string? PrisNote = null, bool ManglerApp = false, string? App = null);
// VoresPris/PrisNote/ManglerApp/App: filled by Indkoeb from the family's apps (AppPriser) - what YOU pay.
// AndenVare: another product than the one searched (kakaomælk, tykmælk, laktosefri for "mælk") - Tilbud decides, the page folds it away.

public sealed record TilbudSvar(IReadOnlyList<TilbudVisning> Tilbud, string? Fejl);

/// <summary>Tilbud could not answer right now (down, slow, or the photoserver is waking up).</summary>
public sealed class TilbudIkkeNu() : Exception("Tilbud svarer ikke lige nu. Prøv igen om lidt.");

public sealed record SeddelGruppe(string Butik, string? Handler, IReadOnlyList<Vare> Varer, DateOnly? Dag = null);

public sealed record KoebVisning(string Id, DateOnly Dato, decimal Beloeb, string Butik, string Hvem, string Type, bool OverGraense);

public sealed record Overblik(string Navn, Maal Maal, UgeStatus Uge, IReadOnlyList<KoebVisning> DenneUge,
    IReadOnlyList<MaanedStatus> Maaneder, IReadOnlyList<string> Butikker, IReadOnlyList<SeddelGruppe> Seddel, string? Sted,
    IReadOnlyList<string> Skjulte, IReadOnlyList<string> Apps, bool Kvittering = false);

/// <summary>Everything the family page shows, plus adding/removing a purchase and changing the goal.</summary>
public sealed class Indkoeb(IHusstandStore store, TimeProvider clock)
{
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private DateOnly Idag => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, Dk));

    public Husstand Opret(string? navn)
    {
        var h = Husstand.Ny(navn, clock.GetUtcNow().UtcDateTime);
        store.Gem(h);
        return h;
    }

    public Overblik? Vis(string noegle)
    {
        if (store.Hent(noegle) is not { } h) return null;
        var idag = Idag;
        var uge = Regler.Uge(h.Koeb, h.Maal, idag);
        var denne = h.Koeb.Where(k => k.Dato >= uge.Mandag).OrderByDescending(k => k.Dato).ThenByDescending(k => k.Oprettet)
            .Select(k => new KoebVisning(k.Id, k.Dato, k.Beloeb, k.Butik, k.Hvem, k.Type(h.Maal) == KoebType.Stort ? "Stort indkøb" : "Supplering",
                k.Type(h.Maal) == KoebType.Supplering && k.Beloeb > h.Maal.SupplGraense)).ToList();
        var first = new DateOnly(idag.Year, idag.Month, 1);
        var maaneder = Enumerable.Range(0, 6).Select(i => first.AddMonths(-i))
            .Select(d => Regler.Maaned(h.Koeb, h.Maal, d.Year, d.Month, idag))
            .Where(ms => ms.Ture > 0 || (ms.Aar == idag.Year && ms.Maaned == idag.Month)).ToList();
        return new Overblik(h.Navn, h.Maal, uge, denne, maaneder, Regler.Butikker, Grupper(h), h.Sted?.Navn, h.Skjulte, h.Apps);
    }

    /// <summary>Error text, or null = saved.</summary>
    public string? LaegInd(string noegle, decimal beloeb, string? butik, string? hvem, DateOnly? dato)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        var idag = Idag;
        var d = dato ?? idag;
        if (Regler.KoebProblem(beloeb, butik, d, idag) is { } p) return p;
        if (h.Koeb.Count >= 5000) h.Koeb.RemoveAll(k => k.Dato < idag.AddYears(-1));
        h.Koeb.Add(new Koeb(Guid.NewGuid().ToString("N")[..10], d, Math.Round(beloeb, 2), Husstand.RenTekst(butik, 30), Husstand.RenTekst(hvem, 30),
            clock.GetUtcNow().UtcDateTime));
        if (d == idag) SeddelRegler.Handlet(h, butik!);   // bought today: what was ticked there leaves the list, the trip is done
        store.Gem(h);
        return null;
    }

    public bool Slet(string noegle, string id)
    {
        if (store.Hent(noegle) is not { } h) return false;
        var n = h.Koeb.RemoveAll(k => k.Id == id);
        if (n > 0) store.Gem(h);
        return n > 0;
    }

    public IReadOnlyList<string> GemMaal(string noegle, Maal m, string? navn)
    {
        if (store.Hent(noegle) is not { } h) return ["Siden findes ikke."];
        var p = m.Problemer();
        if (p.Count > 0) return p;
        h.Maal = m;
        if (Husstand.RenTekst(navn, 40) is { Length: > 0 } n) h.Navn = n;
        store.Gem(h);
        return [];
    }

    // ---- Step 3: Indkøbsseddel (shared). Anyone adds, anyone ticks in the shop. ----

    /// <summary>Groups in the order a shopper needs them: shops first (the offers decide the shop), "Hvor som helst" last.</summary>
    public static IReadOnlyList<SeddelGruppe> Grupper(Husstand h) =>
        h.Seddel.GroupBy(v => SeddelRegler.Gruppe(v.Butik))
            .OrderBy(g => g.Key == SeddelRegler.HvorSomHelst).ThenBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => new SeddelGruppe(g.Key, h.Handler.GetValueOrDefault(g.Key),
                g.OrderBy(v => v.Koebt).ThenBy(v => v.Oprettet).ToList(), h.TurDag.TryGetValue(g.Key, out var d) ? d : null))
            .ToList();

    public string? TilfoejVare(string noegle, string? tekst, string? hvem, string? butik, string? tilbud)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        if (SeddelRegler.Problem(tekst, h.Seddel.Count) is { } p) return p;
        var t = Husstand.RenTekst(tekst, SeddelRegler.MaxTekst);
        var b = Husstand.RenTekst(butik, 30) is { Length: > 0 } x ? x : null;
        if (h.Seddel.Any(v => !v.Koebt && string.Equals(v.Tekst, t, StringComparison.CurrentCultureIgnoreCase)
                              && SeddelRegler.Gruppe(v.Butik) == SeddelRegler.Gruppe(b)))
            return null;   // already on the list - no double lines
        h.Seddel.Add(new Vare(Guid.NewGuid().ToString("N")[..10], t, Husstand.RenTekst(hvem, 30), b,
            Husstand.RenTekst(tilbud, 60) is { Length: > 0 } o ? o : null, false, clock.GetUtcNow().UtcDateTime));
        store.Gem(h);
        return null;
    }

    public bool Afkryds(string noegle, string id, bool koebt)
    {
        if (store.Hent(noegle) is not { } h) return false;
        var i = h.Seddel.FindIndex(v => v.Id == id);
        if (i < 0) return false;
        h.Seddel[i] = h.Seddel[i] with { Koebt = koebt };
        store.Gem(h);
        return true;
    }

    public bool FjernVare(string noegle, string id)
    {
        if (store.Hent(noegle) is not { } h) return false;
        var n = h.Seddel.RemoveAll(v => v.Id == id);
        if (n > 0) store.Gem(h);
        return n > 0;
    }

    /// <summary>"Jeg handler her": someone takes the trip to a shop (one finds the offers, another buys); empty name = let it go.
    /// Dag = when ("tirsdag"); a day can be planned before anyone takes the trip. Null day + no name = the plan is gone.</summary>
    public string? TagTur(string noegle, string? butik, string? hvem, DateOnly? dag = null)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        var g = SeddelRegler.Gruppe(butik);
        var navn = Husstand.RenTekst(hvem, 30);
        if (dag is { } d && (d < Idag || d > Idag.AddDays(31))) return "Vælg en dag fra i dag og en måned frem.";
        if (navn.Length == 0) h.Handler.Remove(g);
        else h.Handler[g] = navn;
        if (dag is { } dd) h.TurDag[g] = dd;
        else if (navn.Length == 0) h.TurDag.Remove(g);
        store.Gem(h);
        return null;
    }

    // ---- Step 2: Find tilbud. Anyone in the family searches; the place is the family's postnummer. ----

    public async Task<string?> SaetStedAsync(string noegle, string? postnr, ITilbudKilde tilbud, CancellationToken ct = default)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        var p = (postnr ?? "").Trim();
        if (!SeddelRegler.GyldigtPostnr(p)) return "Skriv et postnummer med 4 tal, fx 8200.";
        if (await tilbud.StedAsync(p, ct) is not { } sted) return "Postnummeret blev ikke fundet. Tjek det, eller prøv igen om lidt.";
        h = store.Hent(noegle)!;   // re-read: someone may have saved while we waited
        h.Sted = sted;
        store.Gem(h);
        return null;
    }

    public const int MaxTilbud = 30;

    /// <summary>"Vi har Lidl Plus": the family has (or no longer has) a shop app / member card.</summary>
    public string? SaetApp(string noegle, string? app, bool har)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        if (string.IsNullOrWhiteSpace(app)) return "Vælg en app.";
        var n = AppPriser.Navn(Husstand.RenTekst(app, 40));
        h.Apps.RemoveAll(x => string.Equals(x, n, StringComparison.CurrentCultureIgnoreCase));
        if (har && h.Apps.Count < 20) h.Apps.Add(n);
        store.Gem(h);
        return null;
    }

    /// <summary>"Vi handler aldrig i Bilka": hide (or show again) a shop's offers for the whole family.</summary>
    public string? SkjulButik(string noegle, string? butik, bool skjul)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        var g = SeddelRegler.Gruppe(butik);
        if (g == SeddelRegler.HvorSomHelst) return "Vælg en butik.";
        h.Skjulte.RemoveAll(x => string.Equals(x, g, StringComparison.CurrentCultureIgnoreCase));
        if (skjul && h.Skjulte.Count < 40) h.Skjulte.Add(g);
        store.Gem(h);
        return null;
    }

    public async Task<TilbudSvar> SoegTilbudAsync(string noegle, string? q, ITilbudKilde tilbud, CancellationToken ct = default)
    {
        if (store.Hent(noegle) is not { } h) return new([], "Siden findes ikke.");
        if (h.Sted is not { } s) return new([], "Skriv jeres postnummer først, så finder vi butikkerne nær jer.");
        var t = (q ?? "").Trim();
        if (t.Length < 2) return new([], "Skriv mindst 2 bogstaver, fx kaffe.");
        try
        {
            var alle = await tilbud.SoegAsync(Husstand.RenTekst(t, 40), s.Lat, s.Lng, ct);
            // The price YOU pay; app-only offers you cannot use go after the others (still shown: maybe get the app).
            var vist = alle.Where(o => !h.Skjult(o.Butik)).Select(o =>
            {
                var p = AppPriser.For(o.Pris, o.KunMedApp, o.AppNavn, o.AppPris, o.UdenAppPris, h.HarApp);
                return o with { VoresPris = p.Pris, PrisNote = p.Note, ManglerApp = p.ManglerApp, App = p.App };
            }).OrderBy(o => o.AndenVare).ThenBy(o => o.ManglerApp).Take(MaxTilbud).ToList();
            return new(vist, null);
        }
        catch (TilbudIkkeNu e) { return new([], e.Message); }
    }
}
