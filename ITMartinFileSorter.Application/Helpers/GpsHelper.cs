using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ITMartinFileSorter.Application.Helpers;

public static class GpsHelper
{
    public static (double lat, double lng)? GetCoordinates(string path)
    {
        try
        {
            Console.WriteLine($"[GPS CHECK] File: {path}");

            var directories = ImageMetadataReader.ReadMetadata(path);

            // ===== FIRST: normal EXIF GPS (photos) =====
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();

            if (gps != null)
            {
                var latValues = gps.GetRationalArray(GpsDirectory.TagLatitude);
                var lngValues = gps.GetRationalArray(GpsDirectory.TagLongitude);
                var latRef = gps.GetString(GpsDirectory.TagLatitudeRef);
                var lngRef = gps.GetString(GpsDirectory.TagLongitudeRef);

                if (latValues != null &&
                    lngValues != null &&
                    latRef != null &&
                    lngRef != null)
                {
                    double latitude = ToDegrees(latValues);
                    double longitude = ToDegrees(lngValues);

                    if (latRef != "N") latitude *= -1;
                    if (lngRef != "E") longitude *= -1;

                    Console.WriteLine($"[GPS PHOTO] {latitude}, {longitude}");

                    return (latitude, longitude);
                }
            }

            // ===== SECOND: QuickTime / video GPS =====
            var locationTag = directories
                .SelectMany(d => d.Tags)
                .FirstOrDefault(t =>
                    t.Name.Contains("location", StringComparison.OrdinalIgnoreCase) &&
                    t.Description != null);

            if (locationTag != null)
            {
                Console.WriteLine($"[GPS VIDEO RAW] {locationTag.Description}");

                var parsed = ParseIso6709(locationTag.Description);

                if (parsed != null)
                {
                    Console.WriteLine($"[GPS VIDEO] {parsed.Value.lat}, {parsed.Value.lng}");
                    return parsed;
                }
            }

            Console.WriteLine("[GPS] No location found");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPS ERROR] {ex}");
            return null;
        }
    }

    private static double ToDegrees(Rational[] values)
        => values[0].ToDouble()
           + values[1].ToDouble() / 60.0
           + values[2].ToDouble() / 3600.0;

    private static (double lat, double lng)? ParseIso6709(string value)
    {
        try
        {
            // Example:
            // +56.1916+010.1932+071.826/

            var matches = System.Text.RegularExpressions.Regex
                .Matches(value, @"[+-]\d+(\.\d+)?");

            if (matches.Count < 2)
                return null;

            var lat = double.Parse(matches[0].Value,
                System.Globalization.CultureInfo.InvariantCulture);

            var lng = double.Parse(matches[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);

            return (lat, lng);
        }
        catch
        {
            return null;
        }
    }
}