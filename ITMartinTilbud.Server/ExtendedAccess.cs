using System.Security.Cryptography;
using System.Text;

namespace ITMartinTilbud.Server;

/// <summary>Free vs extended (user 2026-10-08): free = "Find tilbud" + "Hvor handler du?" (with radius) + "Godt at vide";
/// Mine varer, Ugen, Madspild and App-kløften are the extended version. Unlocked with the code from Tilbud:PaidCode
/// (Tilbud__PaidCode in magic.env); the browser keeps a cookie with a hash of it. No code configured = nobody unlocks.</summary>
public static class ExtendedAccess
{
    public const string Cookie = "tilbud_udvidet";

    public static string? Token(IConfiguration cfg) =>
        cfg["Tilbud:PaidCode"] is { Length: >= 6 } code
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("tilbud|" + code)))[..32]
            : null;

    public static bool IsUnlocked(HttpContext? ctx, IConfiguration cfg) =>
        Token(cfg) is { } t && ctx?.Request.Cookies[Cookie] == t;

    public static void MapExtendedAccess(this WebApplication app)
    {
        app.MapPost("/udvidet", async (HttpContext ctx, IConfiguration cfg) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var token = Token(cfg);
            var typed = form["kode"].ToString().Trim();
            if (token is null || typed.Length == 0 || cfg["Tilbud:PaidCode"] != typed)
            {
                await Task.Delay(800);   // slows down guessing
                return Results.Redirect("/?kode=forkert#udvidet-kort");
            }
            ctx.Response.Cookies.Append(Cookie, token, new CookieOptions
            {
                HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365),
            });
            return Results.Redirect("/");
        }).DisableAntiforgery();

        app.MapPost("/udvidet/af", (HttpContext ctx) => { ctx.Response.Cookies.Delete(Cookie); return Results.Redirect("/"); }).DisableAntiforgery();
    }
}
