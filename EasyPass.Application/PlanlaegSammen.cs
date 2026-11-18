using EasyPass.Domain;

namespace EasyPass.Application;

/// <summary>A shared Snak room for the trip (Snak's API). Null link = Snak is not set up or did not answer.</summary>
public interface ISnakRum
{
    bool Klar { get; }
    Task<string?> OpretAsync(string titel, IReadOnlyList<string> navne, IReadOnlyList<TurTrin> trin, string slutDag, CancellationToken ct = default);
}

public sealed record PlanlaegResultat(string? Link, string? Fejl);

/// <summary>
/// "Planlæg turen sammen" (2026-11-18, user: Snak rooms for pædagog/user): a Snak room with the person and their helper,
/// the plan as tasks on the right days, and it melts after the trip. Only first names; nothing is stored in Easy-Pass.
/// </summary>
public sealed class PlanlaegSammen(IStedStore steder, ISnakRum snak, TimeProvider clock)
{
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public bool Klar => snak.Klar;

    public async Task<PlanlaegResultat> UdfoerAsync(string stedId, string? behov, string? dato, string? mitNavn, string? hjaelper, CancellationToken ct = default)
    {
        if (!snak.Klar) return new(null, "Det er ikke slået til endnu.");
        if (steder.Hent(stedId) is not { } sted) return new(null, "Stedet findes ikke.");
        var idag = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, Dk));
        if (!DateOnly.TryParseExact((dato ?? "").Trim(), "yyyy-MM-dd", out var dag) || dag < idag || dag > idag.AddYears(1))
            return new(null, "Vælg den dag, I vil af sted (fra i dag og op til et år frem).");
        if (Navn(mitNavn).Length == 0) return new(null, "Skriv dit fornavn.");
        var navne = new[] { mitNavn, hjaelper }.Select(Navn).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var trin = TurTrinRegler.Lav(sted, Katalog.Laes(behov), dag, idag);
        var titel = $"Tur: {sted.Navn} {dag:d.M.}";
        var link = await snak.OpretAsync(titel.Length > 80 ? titel[..80] : titel, navne, trin, dag.AddDays(3).ToString("yyyy-MM-dd"), ct);
        return link is null ? new(null, "Rummet kunne ikke laves lige nu. Prøv igen om lidt.") : new(link, null);
    }

    /// <summary>A first name only: letters, space, hyphen, brackets ("Lise (pædagog)"); max 30.</summary>
    private static string Navn(string? s)
    {
        var n = new string((s ?? "").Trim().Where(c => char.IsLetter(c) || c is ' ' or '-' or '(' or ')').ToArray()).Trim();
        return n.Length > 30 ? n[..30] : n;
    }
}
