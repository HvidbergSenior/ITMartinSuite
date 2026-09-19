using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using ITMartinElPriser.Core;
using ITMartinMitEl.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// Singletons with their own HttpClient: both services cache (prices for
// 30 min, the Eloverblik access token for 12 h) - a transient-per-request
// registration would throw the cache away every call.
builder.Services.AddSingleton(sp => new ElectricityPriceService(new HttpClient(), sp.GetRequiredService<ILogger<ElectricityPriceService>>()));
builder.Services.AddSingleton<HouseholdStore>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddSingleton<RunLogService>();
builder.Services.AddHostedService<NotificationScheduler>();
builder.Services.AddSingleton(sp => new EloverblikService(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, sp.GetRequiredService<ILogger<EloverblikService>>()));
builder.Services.AddSingleton<ConsumptionStore>();
builder.Services.AddSingleton<ConsumptionSync>();
builder.Services.AddSingleton<PriceProfileService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConsumptionSync>());


// PIN gate (phase 1 of the split: family only). The household file holds
// the meter token, devices and run log, so nothing may be served without
// it. Real value comes from magic.env (MitEl__Pin); accounts replace this
// in phase 2.
var pin = builder.Configuration["MitEl:Pin"] ?? "1234";

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "mitel_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(90);
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
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (HttpContext ctx) =>
{
    var showError = ctx.Request.Query.ContainsKey("err");
    var html = $$"""
    <!doctype html><html lang="da"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>MinElpris – Log ind</title>
    <style>
    body{font-family:system-ui,sans-serif;background:#0b1220;color:#e5e7eb;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
    form{background:#111a2e;padding:2rem 2.5rem;border-radius:14px;box-shadow:0 4px 24px rgba(0,0,0,.4);text-align:center}
    input{font-size:1.5rem;padding:.5rem;width:8rem;text-align:center;letter-spacing:.4rem;border-radius:8px;border:1px solid #334155;background:#0b1220;color:#e5e7eb}
    button{display:block;margin:1rem auto 0;font-size:1rem;padding:.55rem 1.5rem;border-radius:8px;border:none;background:#facc15;color:#111;font-weight:700;cursor:pointer}
    .err{color:#f87171;margin-top:.75rem;font-size:.9rem}
    .sub{color:#94a3b8;font-size:.8rem;margin-top:1rem}
    </style></head><body>
    <form method="post" action="/login">
    <div style="font-size:1.6rem">⚡</div><div style="margin-bottom:.75rem;font-weight:700">MinElpris</div>
    <input type="password" name="pin" inputmode="numeric" autofocus autocomplete="off" />
    <button type="submit">Log ind</button>
    {{(showError ? "<div class=\"err\">Forkert PIN</div>" : "")}}
    <div class="sub">Bare priserne? <a href="https://elpriser.itmartin.dk" style="color:#facc15">ElPriser</a> er åben for alle.</div>
    </form></body></html>
    """;
    return Results.Content(html, "text/html");
}).AllowAnonymous();

app.MapPost("/login", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (form["pin"].ToString() != pin)
        return Results.Redirect("/login?err=1");

    // Razor Components' antiforgery system requires every authenticated
    // identity to carry a Name claim, even for a PIN gate with no accounts.
    var claims = new[] { new Claim(ClaimTypes.Name, "household") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// ── Push subscription API (called from wwwroot/js/push.js) ──────────────────

app.MapGet("/api/push/public-key", (PushService push) => Results.Text(push.PublicKey));

app.MapPost("/api/push/subscribe", (SubscribeRequest req, HouseholdStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Endpoint)) return Results.BadRequest();
    PushSubscriber? sub = null;
    store.Update(d =>
    {
        sub = d.Subscribers.FirstOrDefault(s => s.Endpoint == req.Endpoint);
        if (sub is null)
        {
            sub = new PushSubscriber { Endpoint = req.Endpoint };
            d.Subscribers.Add(sub);
        }
        sub.P256dh = req.P256dh;
        sub.Auth = req.Auth;
        sub.Name = string.IsNullOrWhiteSpace(req.Name) ? sub.Name : req.Name.Trim();
        sub.NotifyCheapest = req.NotifyCheapest;
        sub.NotifyExpensive = req.NotifyExpensive;
    });
    return Results.Ok(new { id = sub!.Id });
}).DisableAntiforgery();

app.MapPost("/api/push/unsubscribe", (UnsubscribeRequest req, HouseholdStore store) =>
{
    store.Update(d => d.Subscribers.RemoveAll(s => s.Endpoint == req.Endpoint));
    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/api/push/status", (string endpoint, HouseholdStore store) =>
{
    var s = store.Get().Subscribers.FirstOrDefault(x => x.Endpoint == endpoint);
    return s is null ? Results.NotFound() : Results.Ok(new { s.Name, s.NotifyCheapest, s.NotifyExpensive });
});

// "Send mig en test" - proves the whole chain on this phone right now.
app.MapPost("/api/push/test", async (UnsubscribeRequest req, HouseholdStore store, PushService push, ElectricityPriceService prices) =>
{
    var sub = store.Get().Subscribers.FirstOrDefault(s => s.Endpoint == req.Endpoint);
    if (sub is null) return Results.NotFound();
    var data = store.Get();
    var snap = PriceModel.Build(await prices.GetPricesAsync(data.Settings.PriceArea), data.Settings, data.Appliances, DkTime.Now);
    var body = snap.NowKrPerKwh is { } p ? $"Lige nu koster strømmen {p:0.00} kr/kWh. Beskeder virker ✓" : "Beskeder virker ✓";
    var ok = await push.SendAsync(sub, new PushService.Message("MinElpris", body));
    return ok ? Results.Ok() : Results.StatusCode(410);
}).DisableAntiforgery();

// Plain JSON for anyone who wants the numbers (or for tests).
app.MapGet("/api/snapshot", async (ElectricityPriceService prices, HouseholdStore store) =>
{
    var data = store.Get();
    return Results.Ok(PriceModel.Build(await prices.GetPricesAsync(data.Settings.PriceArea), data.Settings, data.Appliances, DkTime.Now));
});

app.MapRazorComponents<ITMartinMitEl.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth, string? Name, bool NotifyCheapest, bool NotifyExpensive);
public sealed record UnsubscribeRequest(string Endpoint);
