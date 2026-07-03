namespace ITMartinDailyBrief.Server.Models;

public static class DateExtensions
{
    public static string ToTimeAgo(this DateTime dt)
    {
        var diff = DateTime.UtcNow - dt.ToUniversalTime();
        if (diff.TotalMinutes < 2)  return "lige nu";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours} t";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays} dage siden";
        return dt.ToString("d. MMM");
    }
}
