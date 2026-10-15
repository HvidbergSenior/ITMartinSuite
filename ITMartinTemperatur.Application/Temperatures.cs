using ITMartinTemperatur.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinTemperatur.Application;

// A national weather service with real stations (DMI, DWD, later AEMET and Météo-France).
public interface IStationSource
{
    string Name { get; }
    Area Covers { get; }
    Task<IReadOnlyList<Reading>> NearAsync(GeoPoint here, double radiusKm, CancellationToken ct);
}

// A weather model for everywhere else - calculated, never shown as measured.
public interface IModelSource
{
    Task<Reading?> AtAsync(GeoPoint here, CancellationToken ct);
}

public interface IPlaceSearch
{
    Task<IReadOnlyList<Place>> FindAsync(string name, CancellationToken ct);
}

public interface IClock { DateTimeOffset Now { get; } }

public sealed class SystemClock : IClock { public DateTimeOffset Now => DateTimeOffset.UtcNow; }

public sealed class Temperatures(IEnumerable<IStationSource> sources, IModelSource model, IClock clock, ILogger<Temperatures> log)
{
    public async Task<TemperatureHere> HereAsync(GeoPoint here, int radiusKm, CancellationToken ct)
    {
        var radius = TemperatureRules.ClampRadius(radiusKm);
        var box = Area.Around(here, radius);
        // Near a border both countries' stations count, so every source whose area touches the circle is asked.
        var asked = sources.Where(s => Overlaps(s.Covers, box)).ToList();
        var results = await Task.WhenAll(asked.Select(async s =>
        {
            try { return await s.NearAsync(here, radius, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "{Source} failed", s.Name);
                return (IReadOnlyList<Reading>)[];
            }
        }));
        var measured = results.SelectMany(r => r).ToList();
        var now = clock.Now;

        // The model value is always fetched: it is the fallback, and otherwise the comparison with the weather apps.
        Reading? calculated = null;
        try { calculated = await model.AtAsync(here, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Model failed"); }
        var result = TemperatureRules.Decide(measured, calculated, here, radius, now);
        return result.Measured ? result with { Compare = calculated } : result;
    }

    private static bool Overlaps(Area a, Area b) =>
        a.MinLat <= b.MaxLat && b.MinLat <= a.MaxLat && a.MinLon <= b.MaxLon && b.MinLon <= a.MaxLon;
}

public static class DependencyInjection
{
    public static IServiceCollection AddTemperaturApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<Temperatures>();
        return services;
    }
}
