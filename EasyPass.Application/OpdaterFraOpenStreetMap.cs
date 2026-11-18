using EasyPass.Domain;

namespace EasyPass.Application;

public sealed record OsmResultat(int Nye, int Opdateret, int Fjernet, int EksemplerSlettet);

/// <summary>
/// Refresh the places that come from OpenStreetMap. A place someone has edited here (Kilde is no longer "OpenStreetMap")
/// is never overwritten. A place that is gone from OpenStreetMap is removed only if it still is an OpenStreetMap place.
/// The first real import removes the "Eksempel" places.
/// </summary>
public sealed class OpdaterFraOpenStreetMap(IStedStore store, IOpenStreetMapKilde osm, TimeProvider clock)
{
    public async Task<OsmResultat> UdfoerAsync(CancellationToken ct = default)
    {
        var nye = await osm.HentAsync(ct);
        if (nye.Count == 0) return new(0, 0, 0, 0);   // never wipe everything because the service answered empty
        var now = clock.GetUtcNow().UtcDateTime;
        var alle = store.Alle();
        var byOsm = alle.Where(s => s.OsmId is not null).GroupBy(s => s.OsmId!).ToDictionary(g => g.Key, g => g.First());
        int ny = 0, opd = 0;
        var gem = new List<Sted>();
        foreach (var s in nye)
        {
            if (StedRegler.Problemer(s).Count > 0) continue;
            if (byOsm.TryGetValue(s.OsmId!, out var old))
            {
                if (!old.FraOpenStreetMap) continue;   // edited here - ours wins
                s.Id = old.Id;
                opd++;
            }
            else ny++;
            s.Kilde = "OpenStreetMap";
            s.SidstTjekket = now;
            gem.Add(s);
        }
        store.GemMange(gem);

        var stadig = nye.Select(s => s.OsmId).ToHashSet();
        var fjernet = alle.Where(s => s.FraOpenStreetMap && !stadig.Contains(s.OsmId)).Count(s => store.Slet(s.Id));
        var eksempler = alle.Where(s => s.ErEksempel).Count(s => store.Slet(s.Id));
        return new(ny, opd, fjernet, eksempler);
    }
}
