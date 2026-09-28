using System.Text.RegularExpressions;

namespace ITMartinHjem.Server.Services;

// Turns a pasted YouTube / Twitch / Vimeo link (also live streams) into a player address.
// Anything else stays a plain "▶ Se med" link.
public static partial class Embed
{
    public const string Host = "martin.itmartin.dk";

    public static string? PlayerUrl(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link.Trim(), UriKind.Absolute, out var u)) return null;
        var host = u.Host.ToLowerInvariant().Replace("www.", "").Replace("m.", "");

        if (host == "youtu.be") return Yt(u.AbsolutePath.Trim('/'));
        if (host == "youtube.com")
        {
            var v = System.Web.HttpUtility.ParseQueryString(u.Query)["v"];
            if (!string.IsNullOrEmpty(v)) return Yt(v);
            var m = YtPath().Match(u.AbsolutePath);
            if (m.Success) return Yt(m.Groups[1].Value);
        }
        if (host == "twitch.tv")
        {
            var ch = u.AbsolutePath.Trim('/').Split('/')[0];
            if (ch.Length > 0) return $"https://player.twitch.tv/?channel={Uri.EscapeDataString(ch)}&parent={Host}";
        }
        if (host == "vimeo.com" && VimeoId().Match(u.AbsolutePath) is { Success: true } vm)
            return $"https://player.vimeo.com/video/{vm.Groups[1].Value}";
        return null;
    }

    private static string? Yt(string id) =>
        YtId().IsMatch(id) ? $"https://www.youtube-nocookie.com/embed/{id}" : null;

    [GeneratedRegex(@"^/(?:live|shorts|embed)/([\w-]{6,20})")]
    private static partial Regex YtPath();

    [GeneratedRegex(@"^[\w-]{6,20}$")]
    private static partial Regex YtId();

    [GeneratedRegex(@"^/(\d+)")]
    private static partial Regex VimeoId();
}
