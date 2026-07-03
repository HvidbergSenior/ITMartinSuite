namespace ITMartinSuite.Maui.Models;

public class FeedItem
{
    public string   Title       { get; init; } = "";
    public string   Url         { get; init; } = "";
    public string   Description { get; init; } = "";
    public string   ImageUrl    { get; init; } = "";
    public string   SourceName  { get; init; } = "";
    public Color    SourceColor { get; init; } = Colors.Gray;
    public DateTime Published   { get; init; }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - Published.ToUniversalTime();
            if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes} min siden";
            if (diff.TotalHours   < 24)  return $"{(int)diff.TotalHours} t siden";
            if (diff.TotalDays    < 7)   return $"{(int)diff.TotalDays} dage siden";
            return Published.ToString("d. MMM");
        }
    }

    public int ReadMinutes
    {
        get
        {
            var words = Description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(1, (int)Math.Round(words / 200.0));
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
}
