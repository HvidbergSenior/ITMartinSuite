using System.Security.Cryptography;
using System.Text;

namespace ITMartinAdhd.Server;

/// <summary>
/// Find det igen holds one household's things - what they are, where they lie, and photos from inside the home. Until a
/// free version keeps each visitor's things in their own browser, the whole app is for the owner only (2026-10-08: the
/// check found every item, photo and delete open to anyone). PIN from FindIt:Pin (FindIt__Pin in magic.env).
/// Fails closed: no PIN configured = nobody gets in. Only /health stays open (Kontrol checks it).
/// </summary>
public static class PinGate
{
    private const string Cookie = "findit_auth";

    /// <summary>The cookie holds a hash of the PIN, never the PIN itself.</summary>
    private static string Token(string pin) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("findit|" + pin)))[..32];

    public static void UseFindItPinGate(this WebApplication app)
    {
        var pin = app.Configuration["FindIt:Pin"] ?? "";
        var token = pin.Length >= 4 ? Token(pin) : null;

        app.MapGet("/login", (string? fejl) => Results.Content(LoginPage(token is null, fejl is not null), "text/html; charset=utf-8"));
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
            var open = path.Equals("/login", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
            if (open || (token is not null && ctx.Request.Cookies.TryGetValue(Cookie, out var v) && v == token))
            {
                await next();
                return;
            }
            if (path.StartsWith("/api/") || path.StartsWith("/photos/")) { ctx.Response.StatusCode = 401; return; }
            ctx.Response.Redirect("/login");
        });
    }

    private static string LoginPage(bool notConfigured, bool wrong) => $$"""
        <!doctype html><html lang="da"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Find det igen</title>
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css">
        </head><body><main class="k-content" style="max-width:420px;margin:40px auto;padding:0 16px">
        <section class="k-card"><h1 class="k-card-title">🔎 Find det igen</h1>
        <p>Appen er privat – den viser ting og billeder fra et hjem. Skriv koden.</p>
        {{(notConfigured ? "<p class=\"k-help\">Der er ikke sat en kode op endnu.</p>" : "")}}
        {{(wrong ? "<p role=\"alert\"><b>Forkert kode.</b></p>" : "")}}
        <form method="post" action="/login"><input class="k-input" name="pin" type="password" inputmode="numeric" autocomplete="current-password" placeholder="Kode" autofocus>
        <button class="k-btn k-btn-primary" style="margin-top:10px;width:100%">Lås op</button></form></section></main></body></html>
        """;
}
