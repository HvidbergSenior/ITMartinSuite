using EasyPass.Domain;

namespace EasyPass.Application;

public sealed record StedKort(string Id, string Navn, string Kategori, string By, double? Km, string Pasning, string Ord, int Mangler, bool Eksempel);

/// <summary>AntalGodt = how many visitors pressed "Det passede".</summary>
public sealed record StedDetalje(Sted Sted, MatchResultat Match, TurPlan Plan, IReadOnlyList<FacilitetLinje> Faciliteter, int AntalGodt);

public sealed record FacilitetLinje(string Noegle, string Navn, string Svar);

/// <summary>Ved = the town the search was sorted around ("Nærmeste ved Hammel"), or null.</summary>
public sealed record SoegResultat(int Antal, IReadOnlyList<StedKort> Steder, string? Ved = null);

/// <summary>Find steder: text (name/by/postnr), category, "kun dem der passer", nearest first when a position is given.</summary>
public sealed class FindSteder(IStedStore store, IByOpslag byer)
{
    public const int Max = 60;
    public const double RadiusKm = 30;

    /// <summary>
    /// Text that matches a place's name/by/kategori = those places. Text that matches nothing (or a postnummer) = a town:
    /// we look it up and show the nearest places within 30 km. Most places from OpenStreetMap have a position but no address.
    /// </summary>
    public async Task<SoegResultat> UdfoerAsync(string? q, string? kategori, string? behov, bool kunPasser, double? lat, double? lng, CancellationToken ct = default)
    {
        var tekst = (q ?? "").Trim();
        if (tekst.Length > 0 && lat is null)
        {
            var erPostnr = tekst.Length == 4 && tekst.All(char.IsDigit);
            var direkte = Udfoer(tekst, kategori, behov, kunPasser, null, null);
            if (direkte.Antal > 0 && !erPostnr) return direkte;
            if (await byer.FindAsync(tekst, ct) is { } p)
            {
                var naer = Udfoer(null, kategori, behov, kunPasser, p.Lat, p.Lng, RadiusKm);
                return naer with { Ved = p.Navn };
            }
            return direkte;
        }
        return Udfoer(tekst, kategori, behov, kunPasser, lat, lng, lat is null ? null : RadiusKm * 2);
    }

    public SoegResultat Udfoer(string? q, string? kategori, string? behov, bool kunPasser, double? lat, double? lng, double? maxKm = null)
    {
        var mine = Katalog.Laes(behov);
        var tekst = (q ?? "").Trim();
        var kort = new List<StedKort>();

        foreach (var s in store.Alle())
        {
            if (!string.IsNullOrEmpty(kategori) && s.Kategori != kategori) continue;
            if (tekst.Length > 0 && !Rammer(s, tekst)) continue;
            var m = Matcher.Vurder(s, mine);
            if (kunPasser && m.Pasning == Pasning.PasserIkke) continue;
            double? km = lat is { } la && lng is { } lo && s.Lat is { } sl && s.Lng is { } sg ? Math.Round(StedRegler.Km(la, lo, sl, sg), 1) : null;
            if (maxKm is { } mk && (km is null || km > mk)) continue;
            kort.Add(new StedKort(s.Id, s.Navn, s.Kategori, s.By, km, m.Pasning.ToString(), m.Ord, m.Mangler.Count, s.ErEksempel));
        }

        IEnumerable<StedKort> sorted = lat is not null
            ? kort.OrderBy(k => k.Km ?? double.MaxValue)
            : kort.OrderBy(k => k.Pasning switch { "Passer" => 0, "TjekSelv" => 1, _ => 2 }).ThenBy(k => k.Navn);
        return new SoegResultat(kort.Count, sorted.Take(Max).ToList());
    }

    private static bool Rammer(Sted s, string q) =>
        s.Navn.Contains(q, StringComparison.OrdinalIgnoreCase) || s.By.Contains(q, StringComparison.OrdinalIgnoreCase)
        || s.Postnr == q || s.Kategori.Contains(q, StringComparison.OrdinalIgnoreCase);
}

public sealed class VisSted(IStedStore store, ITilbagemeldingStore beskeder)
{
    public StedDetalje? Udfoer(string id, string? behov)
    {
        var s = store.Hent(id);
        if (s is null) return null;
        var linjer = Katalog.Faciliteter.Select(f => new FacilitetLinje(f.Noegle, f.Navn, s.Har(f.Noegle) switch
        {
            Svar.Ja => "Ja", Svar.Nej => "Nej", _ => "Ikke tjekket",
        })).ToList();
        var mine = Katalog.Laes(behov);
        var godt = beskeder.Alle().Count(t => t.StedId == id && t.Type == TilbagemeldingType.Godt);
        return new StedDetalje(s, Matcher.Vurder(s, mine), Planlaegger.Lav(s, mine), linjer, godt);
    }
}

public sealed class SendTilbagemelding(IStedStore steder, ITilbagemeldingStore store, TimeProvider clock)
{
    /// <summary>Error text, or null = saved.</summary>
    public string? Udfoer(string stedId, TilbagemeldingType type, string tekst, string? mail)
    {
        if (steder.Hent(stedId) is null) return "Stedet findes ikke.";
        if (Tilbagemelding.Problem(tekst, mail, type) is { } p) return p;
        store.Gem(new Tilbagemelding
        {
            StedId = stedId, Type = type, Tekst = type == TilbagemeldingType.Godt ? "Det passede" : tekst.Trim(), Mail = string.IsNullOrWhiteSpace(mail) ? null : mail.Trim(),
            Tid = clock.GetUtcNow().UtcDateTime,
        });
        return null;
    }
}

/// <summary>Admin (PIN): save or delete a place, read and close messages.</summary>
public sealed class StedAdmin(IStedStore store, ITilbagemeldingStore beskeder, TimeProvider clock)
{
    public IReadOnlyList<string> Gem(Sted s)
    {
        s.Navn = s.Navn.Trim(); s.By = s.By.Trim(); s.Postnr = s.Postnr.Trim();
        s.Faciliteter = s.Faciliteter.Where(kv => kv.Value != Svar.Ukendt).ToDictionary(kv => kv.Key, kv => kv.Value);
        var p = StedRegler.Problemer(s);
        if (p.Count > 0) return p;
        s.SidstTjekket = clock.GetUtcNow().UtcDateTime;
        store.Gem(s);
        return [];
    }

    public bool Slet(string id) => store.Slet(id);

    public IReadOnlyList<Tilbagemelding> Beskeder() =>
        beskeder.Alle().OrderBy(t => t.Klaret).ThenByDescending(t => t.Tid).ToList();

    public void Klaret(string id)
    {
        var t = beskeder.Alle().FirstOrDefault(x => x.Id == id);
        if (t is null) return;
        t.Klaret = true;
        beskeder.Gem(t);
    }
}
