using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ITMartinHjemmeside.Domain;

namespace ITMartinHjemmeside.Server;

/// <summary>
/// A customer's page as plain server-made HTML: fast on an old phone, readable by Google, no script needed to see it.
/// The customer's own name, colour and logo - the Kolibri look is for Martin's snebolde, not for their brand.
/// </summary>
internal static partial class SiteHtml
{
    /// <param name="basePath">"" on the customer's own domain, "/s/&lt;id&gt;" on the preview address.</param>
    public static string Page(Site site, Page page, string basePath)
    {
        string E(string s) => WebUtility.HtmlEncode(s);
        string Link(Page p) => p.Slug == SiteRules.FrontSlug ? (basePath.Length == 0 ? "/" : basePath + "/") : $"{basePath}/{p.Slug}";
        var title = page.Slug == SiteRules.FrontSlug ? site.Name : $"{page.Title} – {site.Name}";
        var description = site.Tagline.Length > 0 ? site.Tagline : FirstText(page);

        var sb = new StringBuilder();
        sb.Append($$"""
<!DOCTYPE html>
<html lang="da">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{E(title)}}</title>
<meta name="description" content="{{E(description)}}">
<meta name="theme-color" content="{{site.Accent}}">
<style>
:root{--a:{{site.Accent}};--bg:#fbfaf7;--card:#fff;--ink:#1d1d1b;--soft:#5d5d58;--line:#e6e3dc}
@media (prefers-color-scheme:dark){:root{--bg:#161614;--card:#211f1c;--ink:#f1efe9;--soft:#b9b5ab;--line:#35322d} }
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font:18px/1.6 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
header{background:var(--a);color:#fff}
.in{max-width:820px;margin:0 auto;padding:0 16px}
.top{display:flex;align-items:center;gap:12px;padding:16px 0 8px}
.top img{height:48px;width:auto;border-radius:8px;background:#fff}
.top a{color:#fff;text-decoration:none}
.name{font-size:1.5rem;font-weight:700;line-height:1.2}
.tag{opacity:.92;font-size:1rem;padding-bottom:10px}
nav{display:flex;flex-wrap:wrap;gap:8px;padding:6px 0 14px}
nav a{color:#fff;text-decoration:none;padding:8px 14px;border-radius:999px;border:1px solid rgba(255,255,255,.55);font-size:1rem}
nav a.on{background:#fff;color:var(--a);font-weight:600}
main{padding:22px 0 36px}
h1{font-size:1.7rem;line-height:1.25;margin:0 0 14px}
h2{font-size:1.25rem;margin:22px 0 8px}
.box{margin:0 0 18px}
.box p{margin:0 0 10px}
figure{margin:0 0 20px}
figure img{width:100%;height:auto;border-radius:12px;display:block}
figcaption{color:var(--soft);font-size:.95rem;padding-top:6px}
.btns{display:flex;flex-wrap:wrap;gap:10px;margin:8px 0 18px}
.btn{display:inline-block;background:var(--a);color:#fff;text-decoration:none;padding:12px 20px;border-radius:12px;font-weight:600}
footer{border-top:1px solid var(--line);color:var(--soft);font-size:.9rem;padding:18px 0 28px}
</style>
</head>
<body>
<header><div class="in">
<div class="top"><a href="{{Link(site.Pages[0])}}" style="display:flex;align-items:center;gap:12px">
""");
        if (site.Logo is not null) sb.Append($"""<img src="{basePath}/billeder/{site.Logo}" alt="">""");
        sb.Append($"""<span class="name">{E(site.Name)}</span></a></div>""");
        if (site.Tagline.Length > 0) sb.Append($"""<div class="tag">{E(site.Tagline)}</div>""");
        var menu = site.Pages.Where(p => p.InMenu).ToList();
        if (menu.Count > 1)
        {
            sb.Append("<nav>");
            foreach (var p in menu)
                sb.Append($"""<a href="{Link(p)}"{(p == page ? " class=\"on\" aria-current=\"page\"" : "")}>{E(p.Title)}</a>""");
            sb.Append("</nav>");
        }
        sb.Append("</div></header><main><div class=\"in\">");
        if (page.Slug != SiteRules.FrontSlug) sb.Append($"<h1>{E(page.Title)}</h1>");

        foreach (var b in page.Blocks)
        {
            if (b.Kind == BlockKind.Picture && b.Picture is not null)
            {
                sb.Append($"""<figure><img src="{basePath}/billeder/{b.Picture}" alt="{E(b.Text)}" loading="lazy">""");
                if (b.Text.Length > 0) sb.Append($"<figcaption>{E(b.Text)}</figcaption>");
                sb.Append("</figure>");
            }
            else sb.Append("<div class=\"box\">").Append(Text(b.Text)).Append("</div>");
        }

        // The contact page always ends with the buttons (and every page has them in the footer).
        if (page.Slug == "kontakt") sb.Append(ContactButtons(site));
        sb.Append("</div></main><footer><div class=\"in\">");
        sb.Append($"<div><b>{E(site.Name)}</b></div>");
        sb.Append(ContactButtons(site));
        sb.Append("<div>Lavet med Kolibri Hjemmeside · ITMartin</div>");
        sb.Append("</div></footer></body></html>");
        return sb.ToString();
    }

    private static string ContactButtons(Site site)
    {
        if (site.Phone.Length == 0 && site.Mail.Length == 0) return "";
        var sb = new StringBuilder("<div class=\"btns\">");
        if (site.Phone.Length > 0)
            sb.Append($"""<a class="btn" href="tel:{WebUtility.HtmlEncode(site.Phone.Replace(" ", ""))}">Ring {WebUtility.HtmlEncode(site.Phone)}</a>""");
        if (site.Mail.Length > 0)
            sb.Append($"""<a class="btn" href="mailto:{WebUtility.HtmlEncode(site.Mail)}">Skriv en mail</a>""");
        return sb.Append("</div>").ToString();
    }

    private static string FirstText(Page page) =>
        page.Blocks.FirstOrDefault(b => b.Kind == BlockKind.Text)?.Text.Split('\n')[0].TrimStart('#', ' ') is { } t
            ? (t.Length > 150 ? t[..150] : t)
            : page.Title;

    /// <summary>
    /// The owner's text, kept simple on purpose: an empty line = new paragraph, "## " = heading, "- " = list, **fed**.
    /// Everything is HTML-encoded first, so a typed &lt;script&gt; is just text.
    /// </summary>
    internal static string Text(string text)
    {
        var sb = new StringBuilder();
        foreach (var chunk in Regex.Split(text.Replace("\r\n", "\n"), @"\n\s*\n"))
        {
            var lines = chunk.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) continue;
            var i = 0;
            while (i < lines.Count)
            {
                if (lines[i].StartsWith("## "))
                {
                    sb.Append("<h2>").Append(Inline(lines[i][3..])).Append("</h2>");
                    i++;
                }
                else if (lines[i].StartsWith("- "))
                {
                    sb.Append("<ul>");
                    for (; i < lines.Count && lines[i].StartsWith("- "); i++) sb.Append("<li>").Append(Inline(lines[i][2..])).Append("</li>");
                    sb.Append("</ul>");
                }
                else
                {
                    var para = new List<string>();
                    for (; i < lines.Count && !lines[i].StartsWith("## ") && !lines[i].StartsWith("- "); i++) para.Add(Inline(lines[i]));
                    sb.Append("<p>").Append(string.Join("<br>", para)).Append("</p>");
                }
            }
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    private static string Inline(string s) => Bold().Replace(WebUtility.HtmlEncode(s), "<b>$1</b>");
}
