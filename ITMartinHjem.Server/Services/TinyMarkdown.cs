using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace ITMartinHjem.Server.Services;

// Just enough formatting for texts written on the phone:
//   "* " or "- "   -> list          "## "        -> heading
//   **bold**       -> bold          [text](url)  -> link
//   ![alt](url) on its own line -> picture;  other lines -> paragraphs.
// Everything is HTML-encoded first; links only to http(s), /, mailto: and tel:.
public static partial class TinyMarkdown
{
    public static MarkupString Render(string? text)
    {
        var sb = new StringBuilder();
        var inList = false;
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var item = line.StartsWith("* ") || line.StartsWith("- ");
            if (inList && !item) { sb.Append("</ul>"); inList = false; }
            if (line.Length == 0) continue;

            if (Image().Match(line) is { Success: true } img && SafeUrl(img.Groups[2].Value))
            {
                sb.Append("<img class=\"tm-img\" loading=\"lazy\" src=\"").Append(WebUtility.HtmlEncode(img.Groups[2].Value))
                  .Append("\" alt=\"").Append(WebUtility.HtmlEncode(img.Groups[1].Value)).Append("\" />");
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
        if (inList) sb.Append("</ul>");
        return new MarkupString(sb.ToString());
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
