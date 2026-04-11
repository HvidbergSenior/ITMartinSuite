using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Application.Services;

public class HomeLocationService
{
    public (double lat, double lng)? DetectHome(IEnumerable<MediaFile> files)
    {
        var coords = files
            .Select(f => GpsHelper.GetCoordinates(f.FullPath))
            .Where(c => c != null)
            .Select(c => c!.Value)
            .ToList();

        if (!coords.Any())
            return null;

        // Average location = home
        var avgLat = coords.Average(c => c.lat);
        var avgLng = coords.Average(c => c.lng);

        return (avgLat, avgLng);
    }

    public bool IsNearHome(
        (double lat, double lng) home,
        (double lat, double lng) point)
    {
        var distance = DistanceKm(
            home.lat, home.lng,
            point.lat, point.lng);

        return distance < 20; // km
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private static double ToRad(double angle) => angle * Math.PI / 180;
}