using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace ITMartinHjem.Server.Services;

// Just enough formatting for texts written on the phone: "* " or "- " lines become a list,
// **bold** becomes bold, other lines become paragraphs. Everything is HTML-encoded first.
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
            var html = Bold().Replace(WebUtility.HtmlEncode(item ? line[2..] : line), "<b>$1</b>");
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

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();
}
