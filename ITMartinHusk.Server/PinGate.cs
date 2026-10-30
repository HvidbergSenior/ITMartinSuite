using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;

namespace ITMartinHusk.Server;

/// <summary>
/// Husk holds one person's own dates (first Martin's), so it is closed behind a PIN (Husk:Pin = Husk__Pin in magic.env),
/// the same gate as Find det igen. Fails closed: no PIN configured = nobody gets in. Open: /health, the shared look, /om and
/// /hjaelp, the manifest and the service worker (a phone fetches sw.js without the cookie when a push arrives).
/// </summary>
public static class PinGate
{
    private const string Cookie = "husk_auth";
    private static readonly string[] OpenPaths = ["/login", "/om", "/hjaelp", "/manifest.json", "/sw.js", "/icon.svg"];

    /// <summary>The cookie holds a hash of the PIN, never the PIN itself.</summary>
    private static string Token(string pin) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("husk|" + pin)))[..32];

    public static void UseHuskPinGate(this WebApplication app)
    {
        var pin = app.Configuration["Husk:Pin"] ?? "";
        var token = pin.Length >= 4 ? Token(pin) : null;

        app.MapGet("/login", (string? fejl, KolibriAppInfo info) => Results.Content(
            LoginPage(token is null, fejl is not null, KolibriStaticPages.Header(info), KolibriStaticPages.Footer(info)), "text/html; charset=utf-8"));
        app.MapPost("/login", async (HttpContext ctx) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            if (token is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Token(form["pin"].ToString().Trim())), Encoding.UTF8.GetBytes(token)))
            {
                await Task.Delay(800);   // slows down guessing
                return Results.Redirect("/login?fejl=1");
            }
            ctx.Response.Cookies.Append(Cookie, token, new CookieOptions
            {
                HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365),
            });
            return Results.Redirect("/");
        }).DisableAntiforgery();

        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "/";
            var open = OpenPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase))
                       || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/kolibri", StringComparison.OrdinalIgnoreCase);
            if (open || (token is not null && ctx.Request.Cookies.TryGetValue(Cookie, out var v) && v == token))
            {
                await next();
                return;
            }
            if (path.StartsWith("/api/")) { ctx.Response.StatusCode = 401; return; }
            ctx.Response.Redirect("/login");
        });
    }

    private static string LoginPage(bool notConfigured, bool wrong, string header, string footer) => $$"""
        <!doctype html><html lang="da" data-family="puls"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Husk</title>
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css">
        </head><body>{{header}}<main class="k-content" style="max-width:420px;margin:24px auto;padding:0 16px">
        <section class="k-card" data-nofold><h1 class="k-card-title">Husk</h1>
        <p>Husk er privat – den viser dine egne aftaler. Skriv koden.</p>
        {{(notConfigured ? "<p class=\"k-help\">Der er ikke sat en kode op endnu.</p>" : "")}}
        {{(wrong ? "<p role=\"alert\"><b>Forkert kode.</b></p>" : "")}}
        <form method="post" action="/login"><input class="k-input" name="pin" type="password" autocomplete="current-password" placeholder="Kode" autofocus>
        <button class="k-btn k-btn-primary" style="margin-top:10px;width:100%">Lås op</button></form></section></main>{{footer}}</body></html>
        """;
}
