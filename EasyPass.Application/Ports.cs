using EasyPass.Domain;

namespace EasyPass.Application;

public interface IStedStore
{
    IReadOnlyList<Sted> Alle();
    Sted? Hent(string id);
    void Gem(Sted sted);
    void GemMange(IEnumerable<Sted> steder);
    bool Slet(string id);
}

/// <summary>A town or postnummer -> a point (Nominatim). Null = not found or the service is down.</summary>
public sealed record Punkt(string Navn, double Lat, double Lng);

public interface IByOpslag
{
    Task<Punkt?> FindAsync(string tekst, CancellationToken ct = default);
}

/// <summary>Places with accessibility facts from OpenStreetMap (Overpass). Throws when the service cannot be reached.</summary>
public interface IOpenStreetMapKilde
{
    Task<IReadOnlyList<Sted>> HentAsync(CancellationToken ct = default);
}

public interface ITilbagemeldingStore
{
    IReadOnlyList<Tilbagemelding> Alle();
    void Gem(Tilbagemelding t);
}
