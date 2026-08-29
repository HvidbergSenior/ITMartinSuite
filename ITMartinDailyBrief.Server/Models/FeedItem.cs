using System.Security.Cryptography;
using System.Text;

namespace ITMartinDailyBrief.Server.Models;

public class FeedItem
{
    public string   Title       { get; init; } = "";
    public string   Url         { get; init; } = "";
    public string   Description { get; init; } = "";
    public string   ImageUrl    { get; init; } = "";
    public string   AudioUrl    { get; init; } = "";
    public string   SourceName  { get; init; } = "";
    public string   SourceColor { get; init; } = "#6B7280";
    public DateTime Published   { get; init; }

    public string Id => Convert.ToHexString(
        MD5.HashData(Encoding.UTF8.GetBytes(Url)))[..16].ToLower();

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - Published.ToUniversalTime();

            if (diff.TotalMinutes < 2)
                return "lige nu";

            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} min";

            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} t";

            return Published.ToString("d. MMM");
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);

    public bool HasAudio => !string.IsNullOrEmpty(AudioUrl);
}