namespace ITMartinElPriser.Core;

// Wall-clock time in Denmark, regardless of the container's own timezone.
// The NAS container runs in UTC, and with plain DkTime.Now the app showed
// the 16:30 price at 18:30 (2026-09-14) - and would have pushed "an hour
// before the cheapest slot" two hours late.
public static class DkTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateTime FromUtc(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
