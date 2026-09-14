using ITMartinBibliotek.Server;
using ITMartinBibliotek.Server.Data;
using ITMartinBibliotek.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("BibliotekDb")
    ?? "Data Source=/app/data/bibliotek.db";

builder.Services.AddDbContextFactory<BibliotekDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<JellyfinClient>();
builder.Services.AddSingleton<MetadataLookup>();
builder.Services.AddSingleton<JellyfinSync>();

// MusicBrainz and Discogs reject requests without a descriptive User-Agent.
const string userAgent = "ITMartinBibliotek/1.0 (hvidbergsenior@gmail.com)";
foreach (var name in new[] { "jellyfin", "musicbrainz", "discogs", "upc", "tmdb" })
{
    builder.Services.AddHttpClient(name, c =>
    {
        c.Timeout = TimeSpan.FromSeconds(15);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    });
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Bibliotek:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("bibliotek");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BibliotekDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Single-PIN login: the catalog is for the household, sharing of the media
// itself happens inside Jellyfin's own user accounts.
var adminPin = app.Configuration["Bibliotek:AdminPin"] ?? "bibliotek2026";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    if (open || (ctx.Request.Cookies.TryGetValue("bibliotek_auth", out var v) && v == adminPin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin) =>
{
    if (pin == adminPin)
    {
        ctx.Response.Cookies.Append("bibliotek_auth", adminPin, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
        return Results.Redirect("/");
    }
    return Results.Redirect("/login?error=1");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("bibliotek_auth");
    return Results.Redirect("/login");
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
