namespace ITMartinHusk.Domain;

// Wall-clock time in Denmark, regardless of the container's own timezone (same as ElPriser's DkTime).
public static class DkTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
}
