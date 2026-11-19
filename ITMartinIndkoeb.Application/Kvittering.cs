using System.Collections.Concurrent;
using ITMartinIndkoeb.Domain;

namespace ITMartinIndkoeb.Application;

/// <summary>What the receipt said. Gemt = it was saved as a purchase at once (user 2026-11-19: "a picture is enough";
/// a wrong one is removed with Fjern). Not saved = no total found -> the family types the amount.</summary>
public sealed record KvitteringForslag(decimal? Beloeb, string? Butik, DateOnly? Dato, string? Forretning, string? Fejl, bool Gemt = false);

/// <summary>The Kvittering app (ITMartin.Receipt POST /api/scan). Klar = URL + key set.</summary>
public interface IKvitteringLaeser
{
    bool Klar { get; }
    Task<KvitteringForslag> LaesAsync(Stream billede, string filnavn, CancellationToken ct = default);
}

/// <summary>"Tag billede af kvitteringen": one press = one read in Kvittering. At most 10 reads per family per day (each read costs an AI call).</summary>
public sealed class LaesKvittering(IHusstandStore store, IKvitteringLaeser laeser, Indkoeb indkoeb, TimeProvider clock)
{
    public const int MaxPrDag = 10;
    private static readonly ConcurrentDictionary<string, (DateOnly Dag, int Antal)> Brugt = new();

    public bool Klar => laeser.Klar;

    /// <summary>Read the photo and, when the total is found, save it as the purchase straight away (anyone, any time).</summary>
    public async Task<KvitteringForslag> LaesOgGemAsync(string noegle, Stream billede, string filnavn, string? hvem, CancellationToken ct = default)
    {
        var f = await UdfoerAsync(noegle, billede, filnavn, ct);
        if (f.Fejl is not null || f.Beloeb is not > 0) return f;
        var fejl = indkoeb.LaegInd(noegle, f.Beloeb.Value, f.Butik ?? "Andet", hvem, f.Dato);
        return fejl is null ? f with { Gemt = true } : f with { Fejl = fejl };
    }

    public async Task<KvitteringForslag> UdfoerAsync(string noegle, Stream billede, string filnavn, CancellationToken ct = default)
    {
        if (!laeser.Klar) return new(null, null, null, null, "Kvitteringer er ikke slået til endnu. Skriv beløbet selv.");
        if (store.Hent(noegle) is not { } h) return new(null, null, null, null, "Siden findes ikke.");
        var idag = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var ok = true;
        Brugt.AddOrUpdate(h.Id, _ => (idag, 1), (_, v) =>
        {
            var n = v.Dag == idag ? v.Antal : 0;
            if (n >= MaxPrDag) { ok = false; return (idag, n); }
            return (idag, n + 1);
        });
        if (!ok) return new(null, null, null, null, $"I har læst {MaxPrDag} kvitteringer i dag. Skriv beløbet selv resten af dagen.");

        var f = await laeser.LaesAsync(billede, filnavn, ct);
        if (f.Fejl is not null) return f;
        var dato = f.Dato is { } d && d <= idag && d >= idag.AddDays(-62) ? d : (DateOnly?)null;
        return f with { Butik = Regler.ButikFra(f.Forretning), Dato = dato };
    }
}
