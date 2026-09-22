using ITMartin.Shared.UI.Kolibri;
using System.Security.Claims;
using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using ITMartinElPriser.Core;
using ITMartinMitEl.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddKolibri(k =>
{
    k.Name = "MinElpris";
    k.KolibriName = "Kolibri Puls";
    k.Family = "puls";
    k.Tagline = "Dine apparater, din elmåler, din regning – og hvor du kan spare.";
    k.About =
    [
        "MinElpris viser, hvad strømmen koster lige nu og i morgen, og siger til, når den er billig – så vaskemaskine, opvasker og elbil kører på de billige timer.",
        "Med din elmåler koblet på (eloverblik) ser du dit forbrug time for time, og under Enheder finder du de apparater, der trækker mest – også dem der kører om natten, når alt burde være slukket.",
        "Regning forklarer din elregning linje for linje, og Besked sender push til telefonen, når det er tid til at tænde eller slukke.",
    ];
    k.HowTo =
    [
        "Åbn appen fra ikonet på telefonen – forsiden viser prisen nu.",
        "Slå Besked til for at få et prik, når strømmen er billig.",
        "Under Enheder skriver du dine apparater ind – så ser du kr/md pr. apparat.",
        "Har du spørgsmål til regningen, så tryk Regning eller skriv til Martin under Noget virker ikke.",
    ];
    k.Version = "2026.09";
    k.IconPath = "icon.svg";
    k.ThemeColor = "#0b1220";
});
// Singletons with their own HttpClient: both services cache (prices for
// 30 min, the Eloverblik access token for 12 h) - a transient-per-request
// registration would throw the cache away every call.
builder.Services.AddSingleton(sp => new ElectricityPriceService(new HttpClient(), sp.GetRequiredService<ILogger<ElectricityPriceService>>()));
// Accounts and per-household data (phase 2). The stores are resolved per request
// from the signed-in user's household, so a page can only ever read its own home.
var dbPath = builder.Configuration["MitEl:DbPath"]
    ?? Path.Combine(builder.Configuration["DataDir"] ?? "/data", "mitel.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContextFactory<MitElDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<HouseholdRegistry>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<AdvisorService>();
builder.Services.AddScoped<TenantContext>();

builder.Services.AddScoped<HouseholdStore>(sp =>
{
    var registry = sp.GetRequiredService<HouseholdRegistry>();
    var tenant = sp.GetRequiredService<TenantContext>();
    // No household (not signed in, or an advisor who has not picked a customer):
    // an empty throwaway home, so a page never crashes and never shows someone else's data.
    return registry.Household(tenant.CurrentHouseholdId() ?? Guid.Empty);
});

builder.Services.AddScoped<ConsumptionStore>(sp =>
{
    var registry = sp.GetRequiredService<HouseholdRegistry>();
    var tenant = sp.GetRequiredService<TenantContext>();
    return registry.Consumption(tenant.CurrentHouseholdId() ?? Guid.Empty);
});
builder.Services.AddSingleton<PushService>();
builder.Services.AddSingleton<RunLogService>();
builder.Services.AddHostedService<NotificationScheduler>();
builder.Services.AddSingleton(sp => new EloverblikService(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, sp.GetRequiredService<ILogger<EloverblikService>>()));
builder.Services.AddSingleton<ConsumptionSync>();
builder.Services.AddSingleton<PriceProfileService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConsumptionSync>());


// Accounts (phase 2). The household holds the meter token, devices and run log,
// so nothing is served without a login.
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
app.MapKolibri();

// First start after the accounts change: household.json becomes household #1.
StartupMigration.Run(app.Services, app.Configuration, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MitEl"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (HttpContext ctx) => Results.Content(LoginPages.SignIn(
    error: ctx.Request.Query.ContainsKey("err") ? "Forkert mail eller adgangskode." : null,
    notice: ctx.Request.Query.ContainsKey("ny") ? "Kontoen er oprettet – log ind." : null,
    email: ctx.Request.Query["mail"].ToString()), "text/html")).AllowAnonymous();

app.MapPost("/login", async (HttpContext ctx, AccountService accounts) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var result = await accounts.AuthenticateAsync(email, form["password"].ToString());
    if (!result.Ok || result.User is null)
        return Results.Redirect($"/login?err=1&mail={Uri.EscapeDataString(email)}");

    await SignInAsync(ctx, result.User);
    return Results.Redirect(result.User.Role == UserRoles.Advisor ? "/kunder" : "/");
}).AllowAnonymous();

app.MapGet("/opret", (HttpContext ctx) => Results.Content(LoginPages.Register(
    error: ctx.Request.Query["fejl"].ToString() is { Length: > 0 } e ? e : null,
    email: ctx.Request.Query["mail"].ToString()), "text/html")).AllowAnonymous();

app.MapPost("/opret", async (HttpContext ctx, AccountService accounts) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var result = await accounts.RegisterAsync(email, form["password"].ToString(), form["name"].ToString(), form["home"].ToString());

    if (!result.Ok)
        return Results.Redirect($"/opret?fejl={Uri.EscapeDataString(result.Error ?? "Noget gik galt.")}&mail={Uri.EscapeDataString(email)}");

    await SignInAsync(ctx, result.User!);
    return Results.Redirect("/indstillinger");
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete(TenantContext.HouseholdCookie);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// An advisor picking which customer to look at; the grant is checked again on
// every request, so a revoked customer disappears by themselves.
app.MapGet("/vaelg/{householdId:guid}", async (Guid householdId, HttpContext ctx, TenantContext tenant, AdvisorService advisors) =>
{
    if (!tenant.IsAdvisor || tenant.UserId is not { } advisorId) return Results.Forbid();
    if (!await advisors.HasAccessAsync(advisorId, householdId)) return Results.Forbid();

    tenant.SelectHousehold(householdId);
    return Results.Redirect("/");
});

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
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

static async Task SignInAsync(HttpContext ctx, UserAccount user)
{
    // Razor Components' antiforgery system requires a Name claim on every identity.
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Role, user.Role),
    };
    if (user.HouseholdId is { } householdId) claims.Add(new Claim("household", householdId.ToString()));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
}

public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth, string? Name, bool NotifyCheapest, bool NotifyExpensive);
public sealed record UnsubscribeRequest(string Endpoint);
