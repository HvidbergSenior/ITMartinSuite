using ITMartinIndkoeb.Domain;

namespace ITMartinIndkoeb.Application;

public interface IHusstandStore
{
    Husstand? Hent(string noegle);
    void Gem(Husstand h);
}

public sealed record KoebVisning(string Id, DateOnly Dato, decimal Beloeb, string Butik, string Hvem, string Type, bool OverGraense);

public sealed record Overblik(string Navn, Maal Maal, UgeStatus Uge, IReadOnlyList<KoebVisning> DenneUge,
    IReadOnlyList<MaanedStatus> Maaneder, IReadOnlyList<string> Butikker, bool Kvittering = false);

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
        return new Overblik(h.Navn, h.Maal, uge, denne, maaneder, Regler.Butikker);
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
}
