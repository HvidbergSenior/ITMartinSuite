using System.Security.Claims;
using ITMartinElPriser.Core;
using ITMartinLadestander.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton(sp => new ElectricityPriceService(new HttpClient(), sp.GetRequiredService<ILogger<ElectricityPriceService>>()));
builder.Services.AddSingleton<ForeningStore>();
builder.Services.AddSingleton<PollReader>();
builder.Services.AddSingleton<LiveBoard>();
// Live charger data: Zaptec when the association's Zaptec Portal login is in magic.env
// (Ladestander__Zaptec__Username / __Password / __InstallationId), otherwise the simulator.
builder.Services.AddSingleton<IChargerFeed>(sp =>
{
    var z = builder.Configuration.GetSection("Ladestander:Zaptec");
    return string.IsNullOrWhiteSpace(z["Username"]) || string.IsNullOrWhiteSpace(z["InstallationId"])
        ? new SimulatedFeed()
        : new ZaptecFeed(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, z["Username"]!, z["Password"] ?? "", z["InstallationId"]!);
});
builder.Services.AddHostedService<ChargerSync>();

// PIN gate: board only until the supplier decision is made. Real value comes
// from magic.env (Ladestander__Pin).
var pin = builder.Configuration["Ladestander:Pin"] ?? "1234";

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "lade_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (HttpContext ctx) =>
{
    var showError = ctx.Request.Query.ContainsKey("err");
    var html = $$"""
    <!doctype html><html lang="da"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Ladestander – Log ind</title>
    <style>
    body{font-family:system-ui,sans-serif;background:#f6f7f4;color:#1b221e;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
    form{background:#fff;padding:2rem 2.5rem;border-radius:14px;border:1px solid #d8ddd8;box-shadow:0 4px 24px rgba(0,0,0,.08);text-align:center}
    input{font-size:1.5rem;padding:.5rem;width:8rem;text-align:center;letter-spacing:.4rem;border-radius:8px;border:1px solid #c5ccc6}
    button{display:block;margin:1rem auto 0;font-size:1rem;padding:.55rem 1.5rem;border-radius:8px;border:none;background:#1e6b48;color:#fff;font-weight:700;cursor:pointer}
    .err{color:#a33a3a;margin-top:.75rem;font-size:.9rem}
    .sub{color:#5d6862;font-size:.8rem;margin-top:1rem}
    </style></head><body>
    <form method="post" action="/login">
    <div style="font-size:1.6rem">⚡</div><div style="margin-bottom:.75rem;font-weight:700">Ladestander Skelagerhøjen</div>
    <input type="password" name="pin" inputmode="numeric" autofocus autocomplete="off" />
    <button type="submit">Log ind</button>
    {{(showError ? "<div class=\"err\">Forkert PIN</div>" : "")}}
    <div class="sub">Kun for bestyrelsen indtil leverandøren er valgt.</div>
    </form></body></html>
    """;
    return Results.Content(html, "text/html");
}).AllowAnonymous();

app.MapPost("/login", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (form["pin"].ToString() != pin)
        return Results.Redirect("/login?err=1");

    // Razor Components' antiforgery needs a Name claim even for a PIN gate.
    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "bestyrelse")], CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// Plain JSON for tests: prices as the forening sees them.
app.MapGet("/api/snapshot", async (ElectricityPriceService prices, ForeningStore store) =>
{
    var d = store.Get();
    return Results.Ok(PriceModel.Build(await prices.GetPricesAsync(d.Settings.PriceArea), d.Settings, [], DkTime.Now));
});

app.MapRazorComponents<ITMartinLadestander.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
