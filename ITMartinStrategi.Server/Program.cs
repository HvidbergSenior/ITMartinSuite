using ITMartin.Shared.UI.Kolibri;
using ITMartinStrategi.Server;
using ITMartinStrategi.Server.Data;
using ITMartinStrategi.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Strategi";
    k.KolibriName = "Kolibri Navigere";
    k.Family = "navigere";
    k.Tagline = "Forstå spillet og spil bedre – uden at snyde.";
    k.About =
    [
        "Strategi samler guides til de spil, du spiller: hvordan mekanikkerne virker, hvad du gør først, og hvad du gør, når du har valgt noget – en regeringsform, en leder, en strategi.",
        "AI'en skriver et udkast ud fra den nuværende version af spillet. Du retter det til og skriver dine egne strategier ved siden af.",
    ];
    k.HowTo =
    [
        "Vælg et spil.",
        "Tryk på et emne for at få et AI-udkast – eller skriv din egen strategi.",
        "Ret udkastet og marker det som tjekket, når du er enig.",
        "Spørg om noget konkret nederst på spillets side.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("StrategiDb") ?? "Data Source=/app/data/strategi.db";
builder.Services.AddDbContextFactory<StrategiDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<StrategiAi>();

var app = builder.Build();
app.MapKolibri();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<StrategiDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    await Seed.EnsureAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// Single-PIN login: the guides are Martin's own notebook.
var pin = app.Configuration["Strategi:Pin"] ?? "strategi2026";
string[] openPrefixes =
[
    "/_blazor", "/_framework", "/login", "/api/auth", "/kolibri/", "/om", "/hjaelp", "/api/hjaelp",
    "/health", "/manifest.webmanifest", "/kolibri-sw.js", "/_content/",
];
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = openPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
               || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    if (open || (ctx.Request.Cookies.TryGetValue("strategi_auth", out var v) && v == pin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin_) =>
{
    if (pin_ != pin) return Results.Redirect("/login?error=1");
    ctx.Response.Cookies.Append("strategi_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromDays(365),
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("strategi_auth");
    return Results.Redirect("/login");
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
