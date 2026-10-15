namespace ITMartinTemperatur.Domain;

public readonly record struct GeoPoint(double Lat, double Lon)
{
    // Great-circle distance in km (haversine) - plenty precise for "which station is nearest".
    public double KmTo(GeoPoint o)
    {
        const double r = 6371.0;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(o.Lat - Lat);
        var dLon = Rad(o.Lon - Lon);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(Lat)) * Math.Cos(Rad(o.Lat)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public bool IsValid => Lat is >= -90 and <= 90 && Lon is >= -180 and <= 180;
}

// A rough box a source covers, so a position in Spain never asks DMI.
public readonly record struct Area(double MinLat, double MinLon, double MaxLat, double MaxLon)
{
    public bool Contains(GeoPoint p) => p.Lat >= MinLat && p.Lat <= MaxLat && p.Lon >= MinLon && p.Lon <= MaxLon;

    // A box around a point, wide enough for a circle of radiusKm (used for the sources' bbox queries).
    public static Area Around(GeoPoint p, double radiusKm)
    {
        var dLat = radiusKm / 111.0;
        var dLon = radiusKm / (111.0 * Math.Max(0.1, Math.Cos(p.Lat * Math.PI / 180)));
        return new(p.Lat - dLat, p.Lon - dLon, p.Lat + dLat, p.Lon + dLon);
    }
}

// One temperature at one place. Measured = a real thermometer at a weather station;
// otherwise it is calculated by a weather model and must be shown as such.
public sealed record Reading(string Station, GeoPoint Point, double Celsius, DateTimeOffset Observed, string Source, bool Measured);

public sealed record NearbyReading(Reading Reading, double DistanceKm);

// Compare = what the usual weather apps say for the same spot (a model value), next to the measurement.
public sealed record TemperatureHere(NearbyReading? Main, IReadOnlyList<NearbyReading> Nearby, bool Measured, Reading? Compare = null)
{
    // Measured minus the weather apps' value, when there is both a measurement and a model value.
    public double? Difference => Measured && Main is not null && Compare is not null
        ? Math.Round(Main.Reading.Celsius - Compare.Celsius, 1) : null;
}

public static class TemperatureRules
{
    // An observation older than this is not "now".
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(90);

    public static readonly int[] RadiusChoicesKm = [10, 25, 50, 100];

    public static int ClampRadius(int km) => km switch { < 1 => 25, > 100 => 100, _ => km };

    // Newest reading per station, inside the radius, fresh enough - nearest first.
    public static List<NearbyReading> Nearby(IEnumerable<Reading> readings, GeoPoint here, double radiusKm, DateTimeOffset now) =>
        readings
            .Where(r => r.Measured && now - r.Observed <= MaxAge && r.Observed <= now.AddMinutes(5))
            .GroupBy(r => r.Source + "|" + r.Station)
            .Select(g => g.OrderByDescending(r => r.Observed).First())
            .Select(r => new NearbyReading(r, here.KmTo(r.Point)))
            .Where(n => n.DistanceKm <= radiusKm)
            .OrderBy(n => n.DistanceKm)
            .ToList();

    // The nearest measured station inside the radius; without one, the model's value (calculated, not measured).
    public static TemperatureHere Decide(IEnumerable<Reading> measured, Reading? model, GeoPoint here, double radiusKm, DateTimeOffset now)
    {
        var nearby = Nearby(measured, here, radiusKm, now);
        if (nearby.Count > 0) return new(nearby[0], nearby, true);
        return new(model is null ? null : new NearbyReading(model, 0), [], false);
    }
}
