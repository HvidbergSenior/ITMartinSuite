using System.Globalization;
using System.Text.Json;
using EasyPass.Application;
using EasyPass.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace EasyPass.Infrastructure;

/// <summary>
/// Places in Denmark that OpenStreetMap has wheelchair facts for (wheelchair=yes/no/limited), from the free Overpass API.
/// Data © OpenStreetMap contributors, ODbL - the pages say so. One query, run by the weekly job or the admin button.
/// </summary>
internal sealed class OverpassKilde(HttpClient http) : IOpenStreetMapKilde
{
    private const string Query = """
        [out:json][timeout:300];
        area["ISO3166-1"="DK"][admin_level=2]->.dk;
        (
          nwr(area.dk)["name"]["wheelchair"~"^(yes|no|limited)$"]["tourism"~"^(museum|attraction|zoo|aquarium|gallery|theme_park|hotel|hostel|camp_site|viewpoint)$"];
          nwr(area.dk)["name"]["wheelchair"~"^(yes|no|limited)$"]["amenity"~"^(restaurant|cafe|theatre|cinema|library|arts_centre|place_of_worship|community_centre|swimming_pool)$"];
          nwr(area.dk)["name"]["wheelchair"~"^(yes|no|limited)$"]["leisure"~"^(park|nature_reserve|sports_centre|swimming_pool|garden)$"];
        );
        out center tags;
        """;

    public async Task<IReadOnlyList<Sted>> HentAsync(CancellationToken ct = default)
    {
        using var body = new FormUrlEncodedContent([new("data", Query)]);
        using var res = await http.PostAsync("https://overpass-api.de/api/interpreter", body, ct);
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var list = new List<Sted>();
        foreach (var e in doc.RootElement.GetProperty("elements").EnumerateArray())
            if (Map(e) is { } s) list.Add(s);
        return list;
    }

    internal static Sted? Map(JsonElement e)
    {
        if (!e.TryGetProperty("tags", out var t)) return null;
        string Tag(string k) => t.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
        double? lat = null, lng = null;
        if (e.TryGetProperty("lat", out var la)) { lat = la.GetDouble(); lng = e.GetProperty("lon").GetDouble(); }
        else if (e.TryGetProperty("center", out var c)) { lat = c.GetProperty("lat").GetDouble(); lng = c.GetProperty("lon").GetDouble(); }
        var navn = Tag("name");
        if (navn.Length == 0 || lat is null) return null;

        var fac = new Dictionary<string, Svar>();
        var beskrivelse = "";
        switch (Tag("wheelchair"))
        {
            case "yes": fac["niveaufri"] = Svar.Ja; break;
            case "no": fac["niveaufri"] = Svar.Nej; break;
            case "limited": beskrivelse = "Delvis adgang for kørestol - spørg stedet, hvad der går."; break;
        }
        if (JaNej(Tag("toilets:wheelchair")) is { } toilet) fac["toilet"] = toilet;
        if (JaNej(Tag("hearing_loop")) is { } loop) fac["teleslynge"] = loop;
        if (JaNej(Tag("dog")) is { } dog && dog == Svar.Ja) fac["hund"] = Svar.Ja;

        return new Sted
        {
            OsmId = $"{e.GetProperty("type").GetString()}/{e.GetProperty("id").GetInt64()}",
            Navn = navn,
            Kategori = Kategori(Tag("tourism"), Tag("amenity"), Tag("leisure")),
            Beskrivelse = beskrivelse,
            Adresse = string.Join(" ", new[] { Tag("addr:street"), Tag("addr:housenumber") }.Where(x => x.Length > 0)),
            Postnr = Tag("addr:postcode") is { Length: 4 } pc && pc.All(char.IsDigit) ? pc : "",
            By = Tag("addr:city"),
            Lat = Math.Round(lat.Value, 6),
            Lng = Math.Round(lng!.Value, 6),
            Hjemmeside = Tag("website") is { Length: > 0 } w ? w : Tag("contact:website"),
            Telefon = Tag("phone") is { Length: > 0 } p ? p : Tag("contact:phone"),
            Faciliteter = fac,
            Kilde = "OpenStreetMap",
        };
    }

    private static Svar? JaNej(string v) => v switch { "yes" => Svar.Ja, "no" => Svar.Nej, _ => null };

    private static string Kategori(string tourism, string amenity, string leisure) => (tourism, amenity, leisure) switch
    {
        ("museum" or "gallery", _, _) or (_, "arts_centre", _) => "Museum",
        (_, "theatre" or "cinema" or "library" or "community_centre", _) => "Kultur og teater",
        ("hotel" or "hostel" or "camp_site", _, _) => "Overnatning",
        (_, "restaurant" or "cafe", _) => "Spisested",
        (_, "place_of_worship", _) => "Kirke",
        (_, _, "park" or "nature_reserve" or "garden") or ("viewpoint", _, _) => "Natur og park",
        ("attraction" or "zoo" or "aquarium" or "theme_park", _, _) or (_, "swimming_pool", _) or (_, _, "sports_centre" or "swimming_pool") => "Aktivitet",
        _ => "Andet",
    };
}

/// <summary>Town or postnummer -> point, via Nominatim (free, max 1 request a second - cached a week, Danish results only).</summary>
internal sealed class NominatimByOpslag(HttpClient http, IMemoryCache cache) : IByOpslag
{
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    public async Task<Punkt?> FindAsync(string tekst, CancellationToken ct = default)
    {
        tekst = tekst.Trim();
        if (tekst.Length is < 2 or > 60) return null;
        var key = "by|" + tekst.ToLowerInvariant();
        if (cache.TryGetValue(key, out Punkt? hit)) return hit;
        await OneAtATime.WaitAsync(ct);
        try
        {
            var q = tekst.Length == 4 && tekst.All(char.IsDigit) ? $"postalcode={tekst}" : $"q={Uri.EscapeDataString(tekst)}";
            var url = $"https://nominatim.openstreetmap.org/search?{q}&countrycodes=dk&format=json&limit=1";
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            Punkt? p = null;
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var r = doc.RootElement[0];
                var navn = (r.TryGetProperty("name", out var n) ? n.GetString() : null) ?? tekst;
                p = new Punkt(navn,
                    double.Parse(r.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture),
                    double.Parse(r.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture));
            }
            cache.Set(key, p, TimeSpan.FromDays(7));
            return p;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
        finally
        {
            OneAtATime.Release();
        }
    }
}
