using System.Net;
using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinHusk.Application;

namespace ITMartinHusk.Server;

/// <summary>
/// Husk for everyone (2026-11-10): each person sees only their own list.
/// - Martin: his PIN (Husk:Pin = Husk__Pin in magic.env), the same cookie as before - his list is DataDir/husk.json as always.
/// - A customer: the kundenummer (8 digits), checked in Kunder (Husk must be on in Kunder's free apps) - own list.
/// - "Se et eksempel": a made-up list in memory, until the browser is closed or "Luk eksemplet".
/// One field takes both the PIN and the kundenummer. Open without a login: /health, the shared look, /om and /hjaelp, the
/// manifest and the service worker (a phone fetches sw.js without the cookie when a push arrives).
/// Job, Regler and the key API (/api/bolde, /api/job) are Martin's only.
/// </summary>
public static class PinGate
{
    private const string MartinCookie = "husk_auth";
    private const string KundeCookie = "husk_kunde";
    private const string DemoCookie = "husk_eksempel";
    public const string PersonItem = "husk-person";
    public const string PaidItem = "husk-paid";

    private static readonly string[] OpenPaths = ["/login", "/om", "/hjaelp", "/manifest.json", "/sw.js", "/icon.svg", "/eksempel", "/luk-eksempel", "/logud"];
    private static readonly string[] MartinOnly = ["/job", "/regler", "/api/bolde", "/api/job"];

    /// <summary>The cookie holds a hash of the PIN, never the PIN itself.</summary>
    private static string Token(string pin) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("husk|" + pin)))[..32];

    public static void UseHuskPinGate(this WebApplication app)
    {
        var pin = app.Configuration["Husk:Pin"] ?? "";
        var token = pin.Length >= 4 ? Token(pin) : null;
        // Bolde i luften: a helper (Claude, for Martin) updates the balls with this key (Husk:ApiKey = Husk__ApiKey in magic.env).
        var apiKey = app.Configuration["Husk:ApiKey"] ?? "";
        bool KeyOk(HttpContext ctx) => apiKey.Length >= 16 && ctx.Request.Headers.TryGetValue("X-Husk-Key", out var k)
            && Same(k.ToString(), apiKey);

        // The customer and example cookies are signed with a secret made once and kept in the data folder.
        var secret = Secret(app.Configuration["DataDir"] ?? "/data");
        string Sign(string v) => v + "." + Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(v)))[..32];
        string? Unsign(string? c)
        {
            var dot = c?.LastIndexOf('.') ?? -1;
            if (c is null || dot <= 0) return null;
            var v = c[..dot];
            return Same(Sign(v), c) ? v : null;
        }

        string? PersonOf(HttpContext ctx)
        {
            if (Unsign(ctx.Request.Cookies[DemoCookie]) is { } demo && HuskPeople.IsDemo(HuskPeople.Demo(demo))) return HuskPeople.Demo(demo);
            if (token is not null && ctx.Request.Cookies.TryGetValue(MartinCookie, out var m) && Same(m, token)) return HuskPeople.Martin;
            if (Unsign(ctx.Request.Cookies[KundeCookie]) is { } nr && HuskPeople.IsKundenummer(nr)) return HuskPeople.Kunde(nr);
            return null;
        }

        CookieOptions Keep(HttpContext ctx, TimeSpan? age) => new()
        {
            HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = age,
        };

        app.MapGet("/login", (KolibriAppInfo info) => Page(info, ""));
        app.MapPost("/login", async (HttpContext ctx, ICustomerCodes codes, KolibriAppInfo info) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var typed = form["kode"].ToString().Trim();
            if (typed.Length == 0) typed = form["pin"].ToString().Trim();   // a login page that was open before the update
            ctx.Response.Cookies.Delete(DemoCookie);
            if (token is not null && Same(Token(typed), token))
            {
                ctx.Response.Cookies.Append(MartinCookie, token, Keep(ctx, TimeSpan.FromDays(365)));
                return Results.Redirect("/");
            }
            var nr = new string(typed.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
            if (HuskPeople.IsKundenummer(nr))
            {
                var (ok, error, _) = await codes.CheckAsync(nr, ctx.RequestAborted);
                if (ok)
                {
                    ctx.Response.Cookies.Append(KundeCookie, Sign(nr), Keep(ctx, TimeSpan.FromDays(365)));
                    return Results.Redirect("/");
                }
                await Task.Delay(800);   // slows down guessing
                return Page(info, error.Length > 0 ? error : "Det nummer virker ikke i Husk. Spørg Martin.");
            }
            await Task.Delay(800);
            return Page(info, "Dit kundenummer har 8 cifre – du finder det i mailen fra Martin.");
        }).DisableAntiforgery();

        // "Se et eksempel": a fresh made-up list for this browser only (a button on the login page, or a link from itmartin.dk).
        app.MapGet("/eksempel", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Append(DemoCookie, Sign(Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()), Keep(ctx, null));
            return Results.Redirect("/");
        });
        app.MapGet("/luk-eksempel", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(DemoCookie);
            return Results.Redirect("/");
        });
        app.MapGet("/logud", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(DemoCookie);
            ctx.Response.Cookies.Delete(KundeCookie);
            ctx.Response.Cookies.Delete(MartinCookie);
            return Results.Redirect("/login");
        });

        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "/";
            var open = OpenPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase))
                       || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/kolibri", StringComparison.OrdinalIgnoreCase);
            var martinOnly = MartinOnly.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            var person = martinOnly && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && KeyOk(ctx) ? HuskPeople.Martin : PersonOf(ctx);

            if (person is not null)
            {
                if (martinOnly && person != HuskPeople.Martin)
                {
                    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) { ctx.Response.StatusCode = 403; return; }
                    ctx.Response.Redirect("/");
                    return;
                }
                var who = ctx.RequestServices.GetRequiredService<HuskPerson>();
                who.Set(person);
                if (who.Kundenummer is { } knr)
                    who.SetPaid(await ctx.RequestServices.GetRequiredService<ICustomerCodes>().PaidAsync(knr, ctx.RequestAborted));
                // Free vs paid (2026-11-12): Bolde and Alt are Min Husk - a free customer lands on the front page.
                if (!who.MinHusk && HuskParts.IsMinHuskPath(path))
                {
                    ctx.Response.Redirect("/");
                    return;
                }
                ctx.Items[PersonItem] = person;
                ctx.Items[PaidItem] = who.Paid;
                await next();
                return;
            }
            if (open)
            {
                await next();
                return;
            }
            if (path.StartsWith("/api/")) { ctx.Response.StatusCode = 401; return; }
            ctx.Response.Redirect("/login");
        });
    }

    private static bool Same(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static byte[] Secret(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var path = Path.Combine(dataDir, "cookie-secret.txt");
        if (File.Exists(path)) return Convert.FromHexString(File.ReadAllText(path).Trim());
        var made = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(path, Convert.ToHexString(made));
        return made;
    }

    private static IResult Page(KolibriAppInfo info, string error) => Results.Content(
        LoginPage(error, KolibriStaticPages.Header(info), KolibriStaticPages.Footer(info)), "text/html; charset=utf-8");

    private static string LoginPage(string error, string header, string footer) => $$"""
        <!doctype html><html lang="da" data-family="puls"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Husk</title>
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css">
        <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('husk_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
        </head><body>{{header}}<main class="k-content" style="max-width:440px;margin:24px auto;padding:0 16px">
        <section class="k-card" data-nofold><h1 class="k-card-title">Din egen Husk</h1>
        <p><b>Lavet af en med ADHD – til dig med ADHD.</b></p>
        <p>Husk holder styr på dine aftaler og alt det, du har i gang – én ting ad gangen. Kun du kan se din liste.</p>
        <p class="k-help"><b>Sådan gør du:</b> skriv dit kundenummer (8 cifre – det står i mailen fra Martin), og tryk Åbn min Husk.</p>
        {{(error.Length > 0 ? $"<p role=\"alert\"><b>{WebUtility.HtmlEncode(error)}</b></p>" : "")}}
        <form method="post" action="/login">
        <label class="k-label" for="kode">Dit kundenummer</label>
        <input class="k-input" id="kode" name="kode" type="text" autocomplete="username" placeholder="fx 1234 5678" autofocus>
        <button class="k-btn k-btn-primary" style="margin-top:10px;width:100%">Åbn min Husk</button></form></section>
        <section class="k-card" data-nofold><h2 class="k-card-title">Vil du se den først?</h2>
        <p class="k-help">Se en Husk med opdigtede ting. Tryk gerne på alle knapperne – intet bliver gemt.</p>
        <a class="k-btn" style="width:100%" href="/eksempel">Se et eksempel</a>
        <p class="k-help" style="margin-top:14px">Har du ikke et kundenummer? Det er gratis.</p>
        <a class="k-btn k-btn-ghost" style="width:100%" href="https://itmartin.dk/skriv-mig-op">Skriv mig op – hvis du har lyst</a>
        </section></main>{{footer}}</body></html>
        """;
}
