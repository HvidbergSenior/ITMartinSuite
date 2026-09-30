using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace ITMartinHjem.Server.Services;

// Just enough formatting for texts written on the phone:
//   "* " or "- "   -> list          "## "        -> heading
//   **bold**       -> bold          [text](url)  -> link
//   ![alt](url) on its own line -> picture;  other lines -> paragraphs.
//   "### Title | short text" -> a card that folds out (closed by the next ###, ## or a "---" line).
// Everything is HTML-encoded first; links only to http(s), /, mailto: and tel:.
public static partial class TinyMarkdown
{
    public static MarkupString Render(string? text)
    {
        var sb = new StringBuilder();
        var inList = false;
        var inCard = false;
        var media = new List<(string Alt, string Url)>();   // pictures/videos on lines next to each other = one gallery
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var item = line.StartsWith("* ") || line.StartsWith("- ");
            if (inList && !item) { sb.Append("</ul>"); inList = false; }
            if (line.Length == 0) continue;   // a blank line does not break a gallery

            if (Image().Match(line) is { Success: true } img && SafeUrl(img.Groups[2].Value))
            {
                media.Add((img.Groups[1].Value, img.Groups[2].Value));
                continue;
            }
            FlushMedia(sb, media);
            if (line.StartsWith("### ") || line.StartsWith("## ") || line == "---")
            {
                if (inCard) { sb.Append("</div></details>"); inCard = false; }
            }
            if (line == "---") continue;
            if (line.StartsWith("### "))
            {
                var parts = line[4..].Split('|', 2, StringSplitOptions.TrimEntries);
                sb.Append("<details class=\"tm-card\"><summary><span class=\"tm-card-title\">").Append(Inline(parts[0])).Append("</span>");
                if (parts.Length > 1 && parts[1].Length > 0) sb.Append("<span class=\"tm-card-sub\">").Append(Inline(parts[1])).Append("</span>");
                sb.Append("<span class=\"tm-card-more\">Læs mere</span></summary><div class=\"tm-card-body\">");
                inCard = true;
                continue;
            }
            if (line.StartsWith("## "))
            {
                sb.Append("<h2>").Append(Inline(line[3..])).Append("</h2>");
                continue;
            }
            var html = Inline(item ? line[2..] : line);
            if (item)
            {
                if (!inList) { sb.Append("<ul>"); inList = true; }
                sb.Append("<li>").Append(html).Append("</li>");
            }
            else sb.Append("<p>").Append(html).Append("</p>");
        }
        FlushMedia(sb, media);
        if (inList) sb.Append("</ul>");
        if (inCard) sb.Append("</div></details>");
        return new MarkupString(sb.ToString());
    }

    // One picture = full width; several = a grid. A .mp4/.webm/.mov line becomes a video player.
    private static void FlushMedia(StringBuilder sb, List<(string Alt, string Url)> media)
    {
        if (media.Count == 0) return;
        if (media.Count > 1) sb.Append("<div class=\"tm-gallery\">");
        foreach (var (alt, url) in media)
        {
            var u = WebUtility.HtmlEncode(url);
            var a = WebUtility.HtmlEncode(alt);
            if (url.EndsWith(".mp4") || url.EndsWith(".webm") || url.EndsWith(".mov"))
                sb.Append($"<figure class=\"tm-fig\"><video class=\"tm-img\" src=\"{u}\" controls preload=\"metadata\" playsinline></video>");
            else
                sb.Append($"<figure class=\"tm-fig\"><a href=\"{u}\" target=\"_blank\"><img class=\"tm-img\" loading=\"lazy\" src=\"{u}\" alt=\"{a}\" /></a>");
            if (alt.Length > 0) sb.Append($"<figcaption>{a}</figcaption>");
            sb.Append("</figure>");
        }
        if (media.Count > 1) sb.Append("</div>");
        media.Clear();
    }

    private static string Inline(string s)
    {
        var html = Bold().Replace(WebUtility.HtmlEncode(s), "<b>$1</b>");
        return Link().Replace(html, m =>
        {
            var url = WebUtility.HtmlDecode(m.Groups[2].Value);
            if (!SafeUrl(url)) return m.Value;
            var ext = url.StartsWith("http") ? " target=\"_blank\" rel=\"noopener\"" : "";
            return $"<a href=\"{WebUtility.HtmlEncode(url)}\"{ext}>{m.Groups[1].Value}</a>";
        });
    }

    private static bool SafeUrl(string u) =>
        u.StartsWith("https://") || u.StartsWith("http://") || (u.StartsWith('/') && !u.StartsWith("//"))
        || u.StartsWith("mailto:") || u.StartsWith("tel:");

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^!\[([^\]]*)\]\(([^)\s]+)\)$")]
    private static partial Regex Image();
}
