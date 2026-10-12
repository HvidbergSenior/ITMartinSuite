using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ITMartin.Shared.UI.Kolibri;

/// <summary>
/// /om and /hjaelp as plain HTML for apps whose page is a static wwwroot/index.html (no Blazor router, so the
/// shared /kolibri/om and /kolibri/hjaelp components cannot run there). Same header, footer, family and texts as the
/// Blazor apps get. Switch on with <c>k.StaticPages = true</c>; <see cref="Header"/> and <see cref="Footer"/> give the
/// same markup for the app's own index.html.
/// </summary>
public static class KolibriStaticPages
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/om", (KolibriAppInfo info) => Results.Content(Page(info, info.T("Om ", "About ") + info.Name, Om(info)), "text/html; charset=utf-8"))
            .AllowAnonymous();
        app.MapGet("/hjaelp", (KolibriAppInfo info) => Results.Content(Page(info, info.T("Noget virker ikke?", "Something not working?"), Hjaelp(info)), "text/html; charset=utf-8"))
            .AllowAnonymous();
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    public static string Header(KolibriAppInfo info, string? title = null) =>
        $"""<header class="k-header"><div class="k-header-in"><a href="/" style="display:flex;align-items:center;gap:10px;text-decoration:none;color:inherit;min-width:0"><img class="k-logo" src="/{E(info.IconPath.TrimStart('/'))}" alt=""><div class="k-brand"><b>{E(title ?? info.Name)}</b>{(info.KolibriName.Length > 0 ? $"<span>{E(info.KolibriName)}</span>" : "")}</div></a></div></header>""";

    public static string Footer(KolibriAppInfo info) =>
        $"""<footer class="k-footer"><span>&copy; {DateTime.Now.Year} ITMartin · {E(info.T("Den digitale kolibri", "The digital hummingbird"))}</span> <a href="/om">{E(info.T("Om", "About"))} {E(info.Name)}</a> <a href="#k-install" class="k-footer-install">{E(info.T("Læg på telefonen", "Add to phone"))}</a> <a href="/hjaelp">{E(info.T("Noget virker ikke?", "Something not working?"))}</a> <a href="https://itmartin.dk/vilkaar" target="_blank" rel="noopener">{E(info.T("Vilkår & privatliv", "Terms & privacy"))}</a></footer>""";

    private static string Page(KolibriAppInfo info, string title, string body) => $"""
        <!doctype html><html lang="{E(info.Language)}" data-family="{E(info.Family)}"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><title>{E(title)}</title>
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css"><script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{E(info.IconPath.TrimStart('/'))}"></head>
        <body>{Header(info, title)}<div class="k-main"><div class="k-content">
        <a class="k-btn k-btn-sm" href="/" style="margin-bottom:12px">{E(info.T("Tilbage til ", "Back to ") + info.Name)}</a>
        {body}</div></div>{Footer(info)}</body></html>
        """;

    private static string Om(KolibriAppInfo info)
    {
        var sb = new StringBuilder();
        sb.Append($"""<section class="k-card" data-nofold><h1 class="k-card-title">{E(info.Name)}</h1>""");
        if (info.Tagline.Length > 0) sb.Append($"<p><b>{E(info.Tagline)}</b></p>");
        foreach (var p in info.About) sb.Append($"<p>{E(p)}</p>");
        sb.Append("</section>");
        if (info.HowTo.Length > 0)
        {
            sb.Append($"""<section class="k-card" data-nofold><h2 class="k-card-title">{E(info.T("Sådan gør du", "How to"))}</h2><ol>""");
            foreach (var s in info.HowTo) sb.Append($"<li>{E(s)}</li>");
            sb.Append("</ol></section>");
        }
        sb.Append($"""<section class="k-card" data-nofold><h2 class="k-card-title">{E(info.T("Kontakt", "Contact"))}</h2><p>{E(info.ContactName)}<br>{E(info.ContactMail)} · {E(info.ContactPhone)}</p><p class="k-muted">{E(info.T("Version", "Version"))} {E(info.Version)}</p></section>""");
        return sb.ToString();
    }

    private static string Hjaelp(KolibriAppInfo info) => $"""
        <section class="k-card" data-nofold><h1 class="k-card-title">{E(info.T("Noget virker ikke?", "Something not working?"))}</h1>
        <p class="k-help">{E(info.T("Skriv hvad du prøvede, og hvad der skete. Beskeden går direkte til Martin.", "Write what you tried and what happened. The message goes straight to Martin."))}</p>
        <form id="k-hj" method="post" action="/api/hjaelp">
        <input type="hidden" name="page" value="/hjaelp">
        <label class="k-label" for="k-hj-text">{E(info.T("Hvad skete der?", "What happened?"))}</label>
        <textarea class="k-textarea" id="k-hj-text" name="text" rows="5" required></textarea>
        <label class="k-label" for="k-hj-name">{E(info.T("Dit navn (valgfrit)", "Your name (optional)"))}</label>
        <input class="k-input" id="k-hj-name" name="name">
        <label class="k-label" for="k-hj-contact">{E(info.T("Mail eller telefon, hvis du vil have svar", "Mail or phone if you want an answer"))}</label>
        <input class="k-input" id="k-hj-contact" name="contact">
        <button class="k-btn k-btn-primary k-mt" type="submit">{E(info.T("Send", "Send"))}</button>
        <p class="k-muted" id="k-hj-ok" hidden>{E(info.T("Tak – beskeden er sendt.", "Thank you – the message is sent."))}</p>
        </form></section>
        <section class="k-card" data-nofold><h2 class="k-card-title">{E(info.T("Eller ring/skriv", "Or call/write"))}</h2><p>{E(info.ContactMail)} · {E(info.ContactPhone)}</p></section>
        """ + HjaelpScript;

    private const string HjaelpScript = """
        <script>document.getElementById('k-hj').addEventListener('submit',async function(e){e.preventDefault();await fetch('/api/hjaelp',{method:'POST',body:new FormData(this)});this.reset();document.getElementById('k-hj-ok').hidden=false;});</script>
        """;
}
